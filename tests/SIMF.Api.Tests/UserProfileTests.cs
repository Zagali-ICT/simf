using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the user self-service profile endpoints
/// (decisions D-046 b, P8 — D-049; renamed from
/// <c>VisitorProfileTests</c>). Each test signs in a freshly-created
/// Approved user so the QR id is present and the actor can hit the
/// profile endpoints.
/// </summary>
public sealed class UserProfileTests : IClassFixture<SimfApiFactory>
{
    private const string Path = "/api/v1/app/account/user-profile";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public UserProfileTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GET_returns_an_empty_response_with_the_QR_when_no_profile_saved_yet()
    {
        var token = await CreateUserAndSignInAsync(withIdImage: false);

        var response = await GetAuthAsync(Path, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = (await response.Content.ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!;
        Assert.True(body.Success);
        Assert.Empty(body.Data!.ArabicName);
        Assert.Empty(body.Data.EnglishName);
        Assert.False(body.Data.HasIdImage);
        Assert.False(string.IsNullOrEmpty(body.Data.QrId),
            "QR id is minted on Approved (D-046 a) and surfaced here even with no profile row.");
    }

    [Fact]
    public async Task Me_profileComplete_is_false_before_and_true_after_a_full_profile_upsert()
    {
        // D-374 — the app's add-profile-first gate reads this flag off the
        // sign-in hydration; the stub profile row (QR only) must read as
        // incomplete, a full upsert (names + interests) as complete.
        var token = await CreateUserAndSignInAsync();

        var before = await GetAuthAsync("/api/v1/app/users/me", token);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var beforeBody = (await before.Content.ReadFromJsonAsync<ApiResult<CurrentUserResponse>>())!;
        Assert.False(beforeBody.Data!.ProfileComplete);

        var upsert = await PostAuthAsync(Path, await ValidSaudiRequestAsync(), token);
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);

        var after = await GetAuthAsync("/api/v1/app/users/me", token);
        var afterBody = (await after.Content.ReadFromJsonAsync<ApiResult<CurrentUserResponse>>())!;
        Assert.True(afterBody.Data!.ProfileComplete);
    }

    [Fact]
    public async Task POST_upsert_without_an_id_document_reads_incomplete()
    {
        // Two-photo split — the ID DOCUMENT is mandatory for EVERY registrant,
        // enforced via the completeness flag (not a hard upsert reject, so the
        // H16 rollback guarantee + the first-submit transition stay intact). A
        // woman with names + interests but NO ID document saves but reads
        // incomplete, so the app routes her back to finish it.
        var token = await CreateUserAndSignInAsync(withIdImage: false);

        var request = await ValidSaudiRequestAsync();
        request.Gender = Gender.Female;
        var upsert = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);

        var me = await GetAuthAsync("/api/v1/app/users/me", token);
        var body = (await me.Content.ReadFromJsonAsync<ApiResult<CurrentUserResponse>>())!;
        Assert.False(body.Data!.ProfileComplete);
    }

    [Fact]
    public async Task POST_upsert_rejects_a_male_profile_without_a_face_photo()
    {
        // Two-photo split — the FACE photo (avatar) is mandatory for men: a male
        // with the ID document but no face photo is rejected with
        // VISITOR_FACE_IMAGE_MISSING. (The ID is present via the helper.)
        var token = await CreateUserAndSignInAsync();   // ID document uploaded

        var request = await ValidSaudiRequestAsync();
        request.Gender = Gender.Male;
        var upsert = await PostAuthAsync(Path, request, token);

        Assert.Equal(HttpStatusCode.BadRequest, upsert.StatusCode);
        var body = (await upsert.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.VisitorFaceImageMissing, body.Error!.Code);
    }

    [Fact]
    public async Task POST_upsert_accepts_a_male_profile_once_id_and_face_photos_are_uploaded()
    {
        // Two-photo split — the client uploads BOTH photos FIRST (each seeds /
        // sets its path), then the male upsert succeeds and reads complete.
        var token = await CreateUserAndSignInAsync();   // ID document uploaded
        await UploadValidAvatarAsync(token);            // face photo

        var request = await ValidSaudiRequestAsync();
        request.Gender = Gender.Male;
        var upsert = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);

        var me = await GetAuthAsync("/api/v1/app/users/me", token);
        var body = (await me.Content.ReadFromJsonAsync<ApiResult<CurrentUserResponse>>())!;
        Assert.True(body.Data!.ProfileComplete);
    }

    [Fact]
    public async Task POST_upsert_accepts_a_female_profile_without_a_face_photo()
    {
        // Two-photo split — the FACE photo is MALE-only: a woman (with the ID
        // document) saves without one and reads complete. Pins that the face
        // rule is gender-scoped while the ID document is required for all.
        var token = await CreateUserAndSignInAsync();   // ID document uploaded

        var request = await ValidSaudiRequestAsync();
        request.Gender = Gender.Female;
        var upsert = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);

        var me = await GetAuthAsync("/api/v1/app/users/me", token);
        var body = (await me.Content.ReadFromJsonAsync<ApiResult<CurrentUserResponse>>())!;
        Assert.True(body.Data!.ProfileComplete);
    }

    // Name rules — Arabic-only / English-only, full name of at least 2 parts (D-683).

    [Fact]
    public async Task POST_rejects_an_arabic_name_with_non_arabic_characters()
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.ArabicName = "محمد Ahmed عبدالله الزهراني";   // mixed scripts

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_accepts_an_arabic_name_carrying_tashkeel()
    {
        // BUG-021 — the accepted class stopped at U+064A, so an ordinary Arabic
        // name carrying a SHADDA (U+0651) was rejected with "Arabic letters
        // only" — the product's own seed data trips it. Tashkeel is now inside
        // the class; the mixed-script test above still pins that Latin is not.
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.ArabicName = "محمَّد عبدالله الزهراني";   // shadda on the meem

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task POST_rejects_a_single_part_arabic_name()
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.ArabicName = "محمد";   // one part — the floor is 2 (D-683)

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_accepts_a_multi_part_arabic_name()
    {
        // D-683 — the ≥2 rule has no upper cap; an Arabic name can carry more.
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.ArabicName = "محمد عبدالله أحمد";   // three parts

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task POST_accepts_a_two_part_name()
    {
        // D-683 — the floor is 2 parts, so a 2-part name is accepted.
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync("Ahmad Alharbi", "أحمد الحربي");

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task POST_rejects_an_english_name_with_non_english_characters()
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.EnglishName = "Mohammed عبدالله Ahmed Alzahrani";   // mixed scripts

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_rejects_a_single_part_english_name()
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.EnglishName = "Mohammed";   // one part — the floor is 2 (D-683)

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_accepts_a_multi_part_english_name()
    {
        // D-683 — no upper cap on the ≥2 rule.
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.EnglishName = "Mohammed Bin Saleh Ahmed Alzahrani";   // five parts

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task POST_upsert_creates_a_profile_and_a_second_call_updates_it()
    {
        var token = await CreateUserAndSignInAsync();

        var first = await PostAuthAsync(Path,
            await ValidSaudiRequestAsync("Ahmad Bin Saleh Alharbi", "أحمد بن صالح الحربي"), token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = (await first.Content.ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!;
        Assert.Equal("Ahmad Bin Saleh Alharbi", firstBody.Data!.EnglishName);
        Assert.Equal("أحمد بن صالح الحربي", firstBody.Data.ArabicName);
        Assert.Equal("SA", firstBody.Data.NationalityCode);

        var second = await PostAuthAsync(Path,
            await ValidSaudiRequestAsync("Ahmed Bin Saleh Alharbi", "أحمد بن صالح القحطاني"), token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = (await second.Content.ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!;
        Assert.Equal("Ahmed Bin Saleh Alharbi", secondBody.Data!.EnglishName);
        Assert.Equal("أحمد بن صالح القحطاني", secondBody.Data.ArabicName);
    }

    [Fact]
    public async Task POST_rejects_an_unknown_nationality_code()
    {
        var token = await CreateUserAndSignInAsync();

        var request = await ValidSaudiRequestAsync();
        request.NationalityCode = "ZZ";   // not in the curated list

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_requires_a_national_id_for_a_Saudi_user()
    {
        var token = await CreateUserAndSignInAsync();

        var request = await ValidSaudiRequestAsync();
        request.NationalId = null;

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_rejects_a_Saudi_national_id_that_does_not_start_with_1()
    {
        // Saudi national IDs are 10 digits starting with 1 (P5 hardening).
        var token = await CreateUserAndSignInAsync();

        var request = await ValidSaudiRequestAsync();
        request.NationalId = "2234567890";   // 10 digits but starts with 2

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_accepts_a_valid_Iqama_starting_with_2_and_rejects_one_starting_with_1()
    {
        // Iqama numbers are 10 digits starting with 2 (P5 hardening).
        var token = await CreateUserAndSignInAsync();

        var validIqama = await ValidSaudiRequestAsync();
        validIqama.IsSaudi = false;
        validIqama.NationalId = null;
        validIqama.NationalityCode = "AE";
        validIqama.IqamaNumber = "2101798276";   // D-197 — Luhn-valid Iqama

        var ok = await PostAuthAsync(Path, validIqama, token);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var invalidIqama = await ValidSaudiRequestAsync();
        invalidIqama.IsSaudi = false;
        invalidIqama.NationalId = null;
        invalidIqama.NationalityCode = "AE";
        invalidIqama.IqamaNumber = "1234567890";   // starts with 1 — not an Iqama

        var bad = await PostAuthAsync(Path, invalidIqama, token);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task POST_requires_either_Iqama_or_Passport_for_a_non_Saudi_user()
    {
        var token = await CreateUserAndSignInAsync();

        var request = await ValidSaudiRequestAsync();
        request.IsSaudi = false;
        request.NationalId = null;
        request.IqamaNumber = null;
        request.PassportNumber = null;
        request.NationalityCode = "AE";

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_rejects_a_Saudi_national_id_with_an_invalid_checksum()
    {
        // D-197: prefix + length pass, but the Luhn check digit fails.
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.NationalId = "1234567890";   // starts with 1, 10 digits, NOT Luhn-valid

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_rejects_an_Iqama_with_an_invalid_checksum()
    {
        // D-197: prefix + length pass, but the Luhn check digit fails.
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.IsSaudi = false;
        request.NationalId = null;
        request.NationalityCode = "AE";
        request.IqamaNumber = "2345678901";   // starts with 2, 10 digits, NOT Luhn-valid

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_requires_a_date_of_birth()
    {
        // D-197: date of birth is mandatory.
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.DateOfBirth = null;

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_rejects_a_registrant_under_18()
    {
        // D-197: the registrant must be at least 18 years old.
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        // One day short of 18.
        request.DateOfBirth =
            DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-18).AddDays(1);

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_accepts_a_registrant_exactly_18()
    {
        // D-197: exactly 18 today is eligible (boundary).
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-18);

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task POST_accepts_a_valid_passport_for_a_non_Saudi()
    {
        // D-197: passport is a 6-9 alphanumeric format sanity check.
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.IsSaudi = false;
        request.NationalId = null;
        request.NationalityCode = "AE";
        request.PassportNumber = "AB123456";   // 8 alphanumeric

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // C4 (D-371) — the standard phone shapes (supersedes the old
    // permissive-phone test): Saudi 05XXXXXXXX / +9665XXXXXXXX,
    // international E.164; separators are stripped before the match.
    [Theory]
    [InlineData("+44-7700900123")] // international with a dash separator
    [InlineData("+12025550123")]   // US
    [InlineData("00447700900123")] // 00 international prefix → + (owner 2026-07-06)
    public async Task POST_accepts_a_standard_international_phone(string phone)
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.InternationalMobile = phone;

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("+0447700900123")] // leading zero after "+"
    [InlineData("+44")]            // too short
    public async Task POST_rejects_a_non_standard_international_phone(string phone)
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.InternationalMobile = phone;

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("0501234567")]     // local Saudi mobile
    [InlineData("+966501234567")]  // the same number in international form
    [InlineData("050 123-4567")]   // separators stripped before the match
    public async Task POST_accepts_a_standard_Saudi_mobile(string phone)
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.SaudiMobile = phone;

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("0401234567")]    // not the 05 mobile plan
    [InlineData("050123456")]     // 9 digits — too short
    [InlineData("05012345678")]   // 11 digits — too long
    [InlineData("+966401234567")] // +966 but not the 5 mobile prefix
    public async Task POST_rejects_a_non_standard_Saudi_mobile(string phone)
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.SaudiMobile = phone;

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_edit_that_only_changes_the_mobile_persists_it_and_keeps_every_other_field()
    {
        // Owner 2026-07-26 — "Add / Edit phone number in my profile — NO VERIFY,
        // ONLY VALIDATE". The app's My-mobile screen re-POSTs the FULL loaded
        // profile with only the mobile replaced, so this locks the contract it
        // relies on: the existing upsert IS the edit path (no new endpoint, no
        // schema change, no OTP), the new number reads back, and nothing else
        // is nulled by the second save.
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.SaudiMobile = "0501234567";
        var first = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        request.SaudiMobile = "0559876543";
        var edit = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var read = await GetAuthAsync(Path, token);
        var body = (await read.Content.ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!;
        Assert.Equal("0559876543", body.Data!.SaudiMobile);
        Assert.Equal(request.ArabicName, body.Data.ArabicName);
        Assert.Equal(request.EnglishName, body.Data.EnglishName);
        Assert.Equal(request.OrganisationId, body.Data.OrganisationId);
        Assert.Equal(request.NationalId, body.Data.NationalId);
    }

    [Fact]
    public async Task POST_adds_a_mobile_to_a_profile_that_had_none()
    {
        // Owner 2026-07-26 — the "Add" half: a profile saved without a number
        // takes one on a later edit (the field is optional at first save).
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.SaudiMobile = null;
        Assert.Equal(
            HttpStatusCode.OK,
            (await PostAuthAsync(Path, request, token)).StatusCode);

        request.SaudiMobile = "+966501234567";
        Assert.Equal(
            HttpStatusCode.OK,
            (await PostAuthAsync(Path, request, token)).StatusCode);

        var read = await GetAuthAsync(Path, token);
        var body = (await read.Content.ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!;
        Assert.Equal("+966501234567", body.Data!.SaudiMobile);
    }

    // C6 (D-371; relaxed 2026-07-06) — رقم اللوحة: optional, but when present it
    // must be plate letters from the 17-letter set and/or digits (up to 3 + up
    // to 4, separators stripped); the service stores the normalized upper-cased
    // value.
    [Theory]
    [InlineData("ABJ1234", "ABJ1234")]
    [InlineData("abj 1234", "ABJ1234")]   // separators stripped + upper-cased
    [InlineData("1234-ABJ", "1234ABJ")]   // digits-first order
    [InlineData("ابح1234", "ABJ1234")]    // Arabic letters → canonical Latin code
    [InlineData("ابح١٢٣٤", "ABJ1234")]    // Arabic letters + Arabic-Indic digits
    [InlineData("ABJ1", "ABJ1")]          // single digit
    [InlineData("AB1234", "AB1234")]      // 2 letters + digits (relaxed)
    [InlineData("ABJ", "ABJ")]            // letters only (relaxed)
    [InlineData("1234", "1234")]          // digits only (relaxed)
    public async Task POST_accepts_a_standard_plate_and_stores_it_normalized(
        string plate, string stored)
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.PlateNumber = plate;

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content
            .ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!.Data!;
        Assert.Equal(stored, saved.PlateNumber);
    }

    [Theory]
    [InlineData("ABCD123")]   // 4 letters
    [InlineData("ABJ12345")]  // 5 digits
    [InlineData("1234567")]   // 7 digits — more than 4
    [InlineData("AB!1234")]   // symbol
    [InlineData("ABC1234")]   // C is not one of the 17 Saudi plate letters
    [InlineData("ابج1234")]   // ج (jeem) is not a Saudi plate letter
    public async Task POST_rejects_a_non_standard_plate(string plate)
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.PlateNumber = plate;

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_with_no_plate_stays_valid_and_GET_returns_null()
    {
        // C6 — the plate is optional; omitting it never blocks the save.
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();

        var save = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var get = await GetAuthAsync(Path, token);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!.Data!;
        Assert.Null(fetched.PlateNumber);
    }

    [Fact]
    public async Task POST_returns_the_plate_in_both_scripts()
    {
        // C6 — D-459: the stored value is the canonical Latin code; the response
        // also carries the Arabic and English renderings derived from it.
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.PlateNumber = "ابح1234";   // Arabic-script input

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content
            .ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!.Data!;
        Assert.Equal("ABJ1234", saved.PlateNumber);
        Assert.Equal("ABJ1234", saved.PlateNumberEn);
        Assert.Equal("ابح١٢٣٤", saved.PlateNumberAr);
    }

    [Fact]
    public async Task POST_persists_the_job_title_in_both_languages()
    {
        // 2026-07-20 — a visitor can set a bilingual job title; both round-trip so
        // the contact / exhibitor cards + vCard can localize it.
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.JobTitle = "Engineer";
        request.JobTitleArabic = "مهندس";

        var save = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var get = await GetAuthAsync(Path, token);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!.Data!;
        Assert.Equal("Engineer", fetched.JobTitle);
        Assert.Equal("مهندس", fetched.JobTitleArabic);
    }

    // D-373 — the registration reference: SIMF-<year>-<8-digit sequence>,
    // issued once at profile creation, stable across re-saves, unique and
    // monotonic across users.
    [Fact]
    public async Task First_save_issues_a_registration_reference_and_resaves_keep_it()
    {
        var token = await CreateUserAndSignInAsync();

        var first = await PostAuthAsync(Path, await ValidSaudiRequestAsync(), token);
        var raw = await first.Content.ReadAsStringAsync();
        Assert.True(
            first.StatusCode == HttpStatusCode.OK,
            $"expected 200, got {(int)first.StatusCode}: {raw}");
        var firstBody = (await first.Content
            .ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!.Data!;
        Assert.NotNull(firstBody.ReferenceNumber);
        Assert.Matches(@"^SIMF-\d{4}-\d{8}$", firstBody.ReferenceNumber!);

        var second = await PostAuthAsync(Path, await ValidSaudiRequestAsync(), token);
        var secondBody = (await second.Content
            .ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!.Data!;
        Assert.Equal(firstBody.ReferenceNumber, secondBody.ReferenceNumber);
    }

    [Fact]
    public async Task References_are_distinct_and_increasing_across_users()
    {
        var tokenA = await CreateUserAndSignInAsync();
        var tokenB = await CreateUserAndSignInAsync();

        var a = (await (await PostAuthAsync(Path, await ValidSaudiRequestAsync(), tokenA))
            .Content.ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!.Data!;
        var b = (await (await PostAuthAsync(Path, await ValidSaudiRequestAsync(), tokenB))
            .Content.ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!.Data!;

        Assert.NotEqual(a.ReferenceNumber, b.ReferenceNumber);
        var seqA = long.Parse(a.ReferenceNumber![^8..]);
        var seqB = long.Parse(b.ReferenceNumber![^8..]);
        Assert.True(seqB > seqA, $"expected {seqB} > {seqA}");
    }

    // H-1 (FIX A) — the self-service write path must populate the identity blind
    // index (so the filtered UNIQUE indexes + the duplicate-identity guard see it)
    // and reject a National ID / Iqama / passport already on ANOTHER user's row,
    // while never false-flagging a user re-saving their OWN id.
    [Fact]
    public async Task Self_service_upsert_persists_a_non_null_national_id_hash()
    {
        var token = await CreateUserAndSignInAsync();
        var actorId = await GetActorIdAsync(token);

        var response = await PostAuthAsync(Path, await ValidSaudiRequestAsync(), token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hash = await appDb.UserProfiles
            .Where(p => p.UserId == actorId)
            .Select(p => p.NationalIdHash)
            .SingleAsync();
        Assert.False(string.IsNullOrEmpty(hash));
    }

    [Fact]
    public async Task Self_service_upsert_of_an_id_on_another_users_profile_is_409()
    {
        var sharedNationalId = TestIdentity.MintNationalId();

        var tokenA = await CreateUserAndSignInAsync();
        var reqA = await ValidSaudiRequestAsync();
        reqA.NationalId = sharedNationalId;
        var first = await PostAuthAsync(Path, reqA, tokenA);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var tokenB = await CreateUserAndSignInAsync();
        var reqB = await ValidSaudiRequestAsync();
        reqB.NationalId = sharedNationalId;
        var second = await PostAuthAsync(Path, reqB, tokenB);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.DuplicateIdentity, body.Error!.Code);
    }

    [Fact]
    public async Task Self_service_resave_of_my_own_id_is_not_a_false_conflict()
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();

        var first = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Same user, same National ID — self-exclusion means no false 409.
        var second = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    // C5 (D-371) — a self-registering visitor is locked to the single
    // "Normal" audience profile type; richer audience tiers are admin-
    // assigned only, while partner-side ("Other") picks stay free.
    [Fact]
    public async Task POST_rejects_a_non_Normal_audience_profile_type_self_pick()
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.ProfileTypeId = await SeedProfileTypeAsync(
            "VIP — C5 test", "كبار الشخصيات — اختبار", isForVisitor: true);

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_accepts_the_Normal_audience_profile_type_self_pick()
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.ProfileTypeId = await SeedProfileTypeAsync(
            "Normal", "عادي", isForVisitor: true);

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task POST_accepts_a_partner_side_profile_type_self_pick()
    {
        // The "Other" tab lists IsForVisitor=false types — they stay free.
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.ProfileTypeId = await SeedProfileTypeAsync(
            "Media — C5 test", "إعلامي — اختبار", isForVisitor: false);

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // R1 audit fix (D-725) — a CP-only operational partner type
    // (Staff / Moderator, IsAppRegisterable=false) is hidden from the sign-up
    // picker; the self-service write path must reject it too, so a direct POST
    // cannot self-assign an operational ProfileType (which — once an admin
    // approves the account off the "Others" queue — would mint that type's
    // partner MobileAppRole). Fail-closed 400 mirroring the picker filter.
    [Fact]
    public async Task POST_rejects_a_non_app_registerable_partner_profile_type_self_pick()
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.ProfileTypeId = await SeedProfileTypeAsync(
            "Staff — R1 test", "طاقم — اختبار",
            isForVisitor: false, isAppRegisterable: false);

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Profile_completion_replaces_the_email_placeholder_display_name()
    {
        // D-609 (A9c follow-up) — a self-registered account's DisplayName is seeded
        // to its email; completing the profile with a real name replaces it, so the
        // moderator queue + every DisplayName surface stops showing the email.
        var (token, userId, email) = await CreateApprovedVisitorAsync(displayName: null); // == email
        Assert.Equal(email, await GetDisplayNameAsync(userId)); // starts as the placeholder

        var request = await ValidSaudiRequestAsync(englishName: "Real English Name");
        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal("Real English Name", await GetDisplayNameAsync(userId));
    }

    [Fact]
    public async Task Profile_completion_preserves_an_admin_customised_display_name()
    {
        // D-609 — the rename overwrites ONLY the email placeholder; an admin-set
        // DisplayName (!= email) is authoritative and must survive a profile save.
        var (token, userId, _) = await CreateApprovedVisitorAsync(displayName: "Admin Chosen Name");

        var request = await ValidSaudiRequestAsync(englishName: "Real English Name");
        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal("Admin Chosen Name", await GetDisplayNameAsync(userId));
    }

    /// <summary>Finds-or-creates an active profile type by name directly on
    /// the App DB (the C5 lock tests need specific audience/partner rows).</summary>
    private async Task<Guid> SeedProfileTypeAsync(
        string name, string nameArabic, bool isForVisitor,
        bool isAppRegisterable = true)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var existing = await appDb.ProfileTypes
            .SingleOrDefaultAsync(pt => pt.Name == name);
        if (existing is not null)
        {
            return existing.Id;
        }
        var fresh = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = name,
            NameArabic = nameArabic,
            PageColor = "#3B82F6",
            IsForVisitor = isForVisitor,
            MobileAppRole = MobileAppRole.None,
            IsActive = true,
            IsAppRegisterable = isAppRegisterable,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.ProfileTypes.Add(fresh);
        await appDb.SaveChangesAsync();
        return fresh.Id;
    }

    [Fact]
    public async Task ID_image_round_trips_through_encrypted_storage()
    {
        var token = await CreateUserAndSignInAsync();

        // A tiny 1x1 PNG — the smallest valid PNG that passes the magic-byte gate.
        var png = TinyValidPng();

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "File", "id.png");

        using var upload = new HttpRequestMessage(HttpMethod.Post, Path + "/id-image")
        {
            Content = form,
        };
        upload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var uploadResponse = await _client.SendAsync(upload);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        // Fetch back — the bytes must match plaintext.
        using var fetch = new HttpRequestMessage(HttpMethod.Get, Path + "/id-image");
        fetch.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var fetchResponse = await _client.SendAsync(fetch);
        Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);
        Assert.Equal("image/png", fetchResponse.Content.Headers.ContentType?.MediaType);
        var decrypted = await fetchResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(png, decrypted);
    }

    [Fact]
    public async Task ID_image_rejects_a_file_that_is_not_a_real_image()
    {
        var token = await CreateUserAndSignInAsync();

        var fakePng = new byte[] { 0x00, 0x01, 0x02, 0x03 };  // wrong magic bytes
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(fakePng);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "File", "fake.png");

        using var request = new HttpRequestMessage(HttpMethod.Post, Path + "/id-image")
        {
            Content = form,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ID_image_GET_returns_404_when_no_image_set()
    {
        var token = await CreateUserAndSignInAsync(withIdImage: false);

        using var request = new HttpRequestMessage(HttpMethod.Get, Path + "/id-image");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GET_countries_contains_Saudi_Arabia()
    {
        var token = await CreateUserAndSignInAsync();
        var response = await GetAuthAsync(Path + "/countries", token);

        var body = (await response.Content.ReadFromJsonAsync<ApiResult<CountryListResponse>>())!;
        Assert.Contains(body.Data!.Countries, c => c.Code == "SA");
    }

    [Fact]
    public async Task ID_image_is_stored_encrypted_at_rest_in_the_file_store()
    {
        var token = await CreateUserAndSignInAsync();
        var actorId = await GetActorIdAsync(token);

        // Use a distinctive plaintext we can grep the stored bytes for.
        var marker = System.Text.Encoding.UTF8.GetBytes("SIMF-PLAINTEXT-MARKER-2026");
        var image = new byte[3 + marker.Length];
        image[0] = 0xFF; image[1] = 0xD8; image[2] = 0xFF;     // JPEG magic
        Array.Copy(marker, 0, image, 3, marker.Length);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(image);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "File", "id.jpg");
        using var upload = new HttpRequestMessage(HttpMethod.Post, Path + "/id-image")
        {
            Content = form,
        };
        upload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var uploadResponse = await _client.SendAsync(upload);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        // D-568 (S5) — the ID document now lives in the unified StoredFile store as a
        // Confidential, encrypted-at-rest file. Read the RAW stored bytes (skip the
        // decrypt) and confirm the plaintext marker never sits on disk.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var stored = await db.StoredFiles.SingleAsync(
            f => f.Service == FileService.IdDocument && f.OwnerEntityId == actorId && f.IsActive);
        Assert.True(stored.IsEncrypted);
        var storage = scope.ServiceProvider
            .GetRequiredService<SIMF.Application.Files.Abstractions.IFileStorageProvider>();
        var rawOnDisk = await storage.ReadAsync(stored.StorageKey!, encrypted: false, CancellationToken.None);
        Assert.NotNull(rawOnDisk);
        Assert.Equal(-1, IndexOfSequence(rawOnDisk!, marker));
    }

    // -- P9 interests ----------------------------------------------------------

    [Fact]
    public async Task POST_accepts_an_empty_interest_list()
    {
        // D-684 — the profile is now saved BEFORE interests are picked
        // (profile-first save), so a profile save may legitimately carry 0
        // interests; the interests are added in the second save. The interests
        // screen still requires 1-10 client-side.
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.InterestIds = new List<Guid>();   // empty is now allowed server-side

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task POST_rejects_more_than_ten_interests()
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        // Eleven random ids — the validator gates before the service.
        request.InterestIds = Enumerable.Range(0, 11)
            .Select(_ => Guid.NewGuid())
            .ToList();

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_rejects_an_unknown_interest_id()
    {
        var token = await CreateUserAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.InterestIds = new List<Guid> { Guid.NewGuid() };   // never seeded

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.InterestInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task POST_rejects_a_deactivated_interest_id()
    {
        var token = await CreateUserAndSignInAsync();

        // Seed an interest then deactivate it directly.
        Guid deactivatedId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var interest = new UserInterest
            {
                Id = Guid.NewGuid(),
                Name = $"Deactivated {Guid.NewGuid():N}",
                NameArabic = "اهتمام معطّل",
                DisplayOrder = 0,
                IsActive = false,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            appDb.Interests.Add(interest);
            await db.SaveChangesAsync();
            await appDb.SaveChangesAsync();
            deactivatedId = interest.Id;
        }

        var request = await ValidSaudiRequestAsync();
        request.InterestIds = new List<Guid> { deactivatedId };

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.InterestInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task POST_round_trips_picked_interests()
    {
        var token = await CreateUserAndSignInAsync();

        // Seed three interests; pick two.
        Guid one, two, three;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var rows = Enumerable.Range(0, 3)
                .Select(i => new UserInterest
                {
                    Id = Guid.NewGuid(),
                    Name = $"Pick {i} {Guid.NewGuid():N}",
                    NameArabic = $"اختيار {i}",
                    DisplayOrder = i,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                })
                .ToList();
            appDb.Interests.AddRange(rows);
            await db.SaveChangesAsync();
            await appDb.SaveChangesAsync();
            one = rows[0].Id; two = rows[1].Id; three = rows[2].Id;
        }

        var request = await ValidSaudiRequestAsync();
        request.InterestIds = new List<Guid> { one, two };

        var first = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = (await first.Content.ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!;
        Assert.Equal(2, firstBody.Data!.InterestIds.Count);
        Assert.Contains(one, firstBody.Data.InterestIds);
        Assert.Contains(two, firstBody.Data.InterestIds);

        // Second upsert — replace { one, two } with { two, three }; the
        // diff-then-save in the service removes `one` and adds `three`.
        request.InterestIds = new List<Guid> { two, three };
        var second = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = (await second.Content.ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!;
        Assert.Equal(2, secondBody.Data!.InterestIds.Count);
        Assert.DoesNotContain(one, secondBody.Data.InterestIds);
        Assert.Contains(two, secondBody.Data.InterestIds);
        Assert.Contains(three, secondBody.Data.InterestIds);
    }

    // ----------------------------------------------------------------------
    // H2 — D-057: the auto-transition + refresh-token revoke are now
    // transactional, so a first-time profile submit either commits all
    // three writes (profile rows, AccountState change, refresh-token
    // revocations) or none.
    // ----------------------------------------------------------------------

    [Fact]
    public async Task First_profile_save_flips_state_to_PendingApproval_and_revokes_every_refresh_token()
    {
        var (token, userId) = await CreateEmailVerifiedVisitorAndSignInAsync();

        // Pre-condition: the sign-in minted at least one live refresh token.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            var live = await db.RefreshTokens
                .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
                .CountAsync();
            Assert.True(live > 0,
                "Expected the sign-in to mint at least one live refresh token.");
        }

        var request = await ValidSaudiRequestAsync();
        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

            // State flipped.
            var user = await db.Users.SingleAsync(u => u.Id == userId);
            Assert.Equal(AccountState.PendingApproval, user.AccountState);
            Assert.NotNull(user.StateChangedAt);

            // Every refresh token for this user is now revoked.
            var stillLive = await db.RefreshTokens
                .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
                .CountAsync();
            Assert.Equal(0, stillLive);
        }
    }

    private async Task<(string Token, Guid UserId)> CreateEmailVerifiedVisitorAndSignInAsync(
        bool withIdImage = true)
    {
        var email = $"ev-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "EmailVerified Visitor",
                AccountState = AccountState.EmailVerified,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;
        }

        var sign = await _client.PostAsJsonAsync("/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.Web,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        Assert.True(body.Success, "EmailVerified user should be able to sign in (D-010).");
        var token = body.Data!.Tokens!.AccessToken;
        // The ID document is mandatory for every upsert (two-photo split); the
        // upload seeds the profile stub. Tests that seed their own stub row pass
        // withIdImage: false and upload after seeding to avoid a duplicate row.
        if (withIdImage)
        {
            await UploadValidIdImageAsync(token);
        }
        return (token, userId);
    }

    // -- D-190 — ProfileTypeId on UpsertUserProfileRequest --------------------

    [Fact]
    public async Task Upsert_with_ProfileTypeId_round_trips_to_GET()
    {
        // D-190: the user self-picks a ProfileType from the public
        // picker on Screen 2; the upsert persists it; GET reflects
        // it back so the mobile can render the selected row. C5 (D-371):
        // the only valid audience self-pick is the "Normal" type.
        var (token, _) = await CreateEmailVerifiedVisitorAndSignInAsync();
        var profileTypeId = await SeedProfileTypeAsync(
            "Normal", "عادي", isForVisitor: true);

        var request = await ValidSaudiRequestAsync();
        request.ProfileTypeId = profileTypeId;
        var save = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var saved = (await save.Content
            .ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!.Data!;
        Assert.Equal(profileTypeId, saved.ProfileTypeId);

        var get = await GetAuthAsync(Path, token);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!.Data!;
        Assert.Equal(profileTypeId, fetched.ProfileTypeId);
    }

    [Fact]
    public async Task Upsert_with_unknown_ProfileTypeId_returns_400()
    {
        var (token, _) = await CreateEmailVerifiedVisitorAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.ProfileTypeId = Guid.NewGuid();   // never seeded

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AdminProfileTypeInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Upsert_with_inactive_ProfileTypeId_returns_400()
    {
        var (token, _) = await CreateEmailVerifiedVisitorAndSignInAsync();
        var dormantId = await SeedProfileTypeAsync(isVisitor: true, isActive: false);

        var request = await ValidSaudiRequestAsync();
        request.ProfileTypeId = dormantId;

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AdminProfileTypeInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Admin_preassigned_ProfileTypeId_wins_over_user_self_pick()
    {
        // D-190: when the admin pre-assigned a ProfileType (e.g.
        // via /admin/others), the user's self-pick on the upsert is
        // silently ignored. The admin's row stays. C5 (D-371): the
        // user's pick must itself be a valid self-pick (Normal) to get
        // past validation — admin-wins is then decided downstream.
        // This test seeds its OWN stub UserProfile, so opt out of the helper's
        // ID upload (which would seed a duplicate row) and upload after seeding.
        var (token, userId) = await CreateEmailVerifiedVisitorAndSignInAsync(
            withIdImage: false);
        var adminAssigned = await SeedProfileTypeAsync(isVisitor: false);
        var userPickedDifferent = await SeedProfileTypeAsync(
            "Normal", "عادي", isForVisitor: true);

        // Seed a stub UserProfile with the admin's pre-assigned ProfileTypeId.
        using (var scope = _factory.Services.CreateScope())
        {
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            appDb.UserProfiles.Add(new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProfileTypeId = adminAssigned,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await appDb.SaveChangesAsync();
        }

        // Two-photo split — the ID document is mandatory for the upsert; the
        // stub now exists, so this just sets its path.
        await UploadValidIdImageAsync(token);

        var request = await ValidSaudiRequestAsync();
        request.ProfileTypeId = userPickedDifferent;
        var save = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var saved = (await save.Content
            .ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!.Data!;
        // Admin's pre-assigned value survives.
        Assert.Equal(adminAssigned, saved.ProfileTypeId);
        Assert.NotEqual(userPickedDifferent, saved.ProfileTypeId);
    }

    private async Task<Guid> SeedProfileTypeAsync(
        bool isVisitor, bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = $"UpsertTier-{Guid.NewGuid():N}",
            NameArabic = "اختبار",
            PageColor = "#1F2937",
            IsForVisitor = isVisitor,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.ProfileTypes.Add(row);
        await appDb.SaveChangesAsync();
        return row.Id;
    }

    // -- B3 — D-221: الجهة (Organisation) + الجنس (Gender) --------------------

    [Fact]
    public async Task Upsert_with_OrganisationId_and_Gender_round_trips_to_GET()
    {
        var (token, _) = await CreateEmailVerifiedVisitorAndSignInAsync();
        var organisationId = await SeedOrganisationAsync();

        // Two-photo split — a male profile needs BOTH the ID document (uploaded
        // by the helper) and the face photo on the server first.
        await UploadValidAvatarAsync(token);

        var request = await ValidSaudiRequestAsync();
        request.OrganisationId = organisationId;
        request.Gender = Gender.Male;

        var save = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var saved = (await save.Content
            .ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!.Data!;
        Assert.Equal(organisationId, saved.OrganisationId);
        Assert.Equal(Gender.Male, saved.Gender);

        var get = await GetAuthAsync(Path, token);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!.Data!;
        Assert.Equal(organisationId, fetched.OrganisationId);
        Assert.Equal(Gender.Male, fetched.Gender);
    }

    [Fact]
    public async Task Upsert_with_unknown_OrganisationId_returns_400()
    {
        var (token, _) = await CreateEmailVerifiedVisitorAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.OrganisationId = Guid.NewGuid();   // never seeded

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.OrganisationInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Upsert_without_OrganisationId_returns_400()
    {
        // B3 — D-221: organisation is required for every registrant.
        var (token, _) = await CreateEmailVerifiedVisitorAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.OrganisationId = null;

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upsert_with_inactive_OrganisationId_returns_400()
    {
        var (token, _) = await CreateEmailVerifiedVisitorAndSignInAsync();
        var dormantId = await SeedOrganisationAsync(isActive: false);

        var request = await ValidSaudiRequestAsync();
        request.OrganisationId = dormantId;

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.OrganisationInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Upsert_with_an_out_of_range_Gender_returns_400()
    {
        var (token, _) = await CreateEmailVerifiedVisitorAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.Gender = (Gender)99;   // not a defined enum value

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -- D-611 (Wave B): المنطقة (Region) — the schema-ready column is now wired
    // through the self-service upsert (was persisted nowhere before). ---------

    [Fact]
    public async Task Upsert_with_RegionId_round_trips_to_GET()
    {
        var (token, _) = await CreateEmailVerifiedVisitorAndSignInAsync();
        var regionId = await SeedRegionAsync();

        // Male profile needs the face photo on the server first (two-photo split).
        await UploadValidAvatarAsync(token);

        var request = await ValidSaudiRequestAsync();
        request.RegionId = regionId;
        request.Gender = Gender.Male;

        var save = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var saved = (await save.Content
            .ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!.Data!;
        Assert.Equal(regionId, saved.RegionId);

        var get = await GetAuthAsync(Path, token);
        var fetched = (await get.Content
            .ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!.Data!;
        Assert.Equal(regionId, fetched.RegionId);
    }

    [Fact]
    public async Task Upsert_without_RegionId_is_allowed_and_stays_null()
    {
        // المنطقة is optional (unlike الجهة) — a save that omits it succeeds.
        var (token, _) = await CreateEmailVerifiedVisitorAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.RegionId = null;

        var save = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var saved = (await save.Content
            .ReadFromJsonAsync<ApiResult<UserProfileResponse>>())!.Data!;
        Assert.Null(saved.RegionId);
    }

    [Fact]
    public async Task Upsert_with_unknown_RegionId_returns_400()
    {
        var (token, _) = await CreateEmailVerifiedVisitorAndSignInAsync();
        var request = await ValidSaudiRequestAsync();
        request.RegionId = Guid.NewGuid();   // never seeded

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.RegionInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Upsert_with_inactive_RegionId_returns_400()
    {
        var (token, _) = await CreateEmailVerifiedVisitorAndSignInAsync();
        var dormantId = await SeedRegionAsync(isActive: false);

        var request = await ValidSaudiRequestAsync();
        request.RegionId = dormantId;

        var response = await PostAuthAsync(Path, request, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.RegionInvalid, body.Error!.Code);
    }

    private async Task<Guid> SeedRegionAsync(bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = new SIMF.Domain.Regions.Region
        {
            Id = Guid.NewGuid(),
            Code = "R-" + Guid.NewGuid().ToString("N")[..8],
            NameArabic = $"منطقة اختبار {Guid.NewGuid():N}",
            Name = $"Test Region {Guid.NewGuid():N}",
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.Regions.Add(row);
        await appDb.SaveChangesAsync();
        return row.Id;
    }

    private async Task<Guid> SeedOrganisationAsync(bool isActive = true)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var row = new SIMF.Domain.Organisations.Organisation
        {
            Id = Guid.NewGuid(),
            NameArabic = $"جهة اختبار {Guid.NewGuid():N}",
            Name = $"Test Organisation {Guid.NewGuid():N}",
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.Organisations.Add(row);
        await appDb.SaveChangesAsync();
        return row.Id;
    }

    // -- Helpers ---------------------------------------------------------------

    /// <summary>A tiny 1x1 PNG — the smallest valid PNG that passes the
    /// magic-byte gate. Shared by the id-image round-trip test and the
    /// male-photo helper.</summary>
    private static byte[] TinyValidPng() => new byte[]
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82,
    };

    /// <summary>C7 (D-371 / D-431) — uploads a valid tiny PNG to the id-image
    /// endpoint (the base factory runs with the face gate OFF) so a male
    /// profile can satisfy the mandatory-photo rule before the upsert.</summary>
    private async Task UploadValidIdImageAsync(string token)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(TinyValidPng());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "File", "id.png");
        using var upload = new HttpRequestMessage(HttpMethod.Post, Path + "/id-image")
        {
            Content = form,
        };
        upload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(upload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Two-photo split — uploads a valid tiny PNG to the avatar
    /// endpoint so a MALE profile satisfies the mandatory face-photo rule before
    /// the upsert (the base factory has no server face gate on the avatar).</summary>
    private async Task UploadValidAvatarAsync(string token)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(TinyValidPng());
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "File", "face.png");
        using var upload = new HttpRequestMessage(HttpMethod.Post, "/api/v1/app/account/avatar")
        {
            Content = form,
        };
        upload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(upload);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<UpsertUserProfileRequest> ValidSaudiRequestAsync(
        // Names must be at least two parts in one script (Arabic-only / English-only).
        string englishName = "Test Visitor User Account",
        string arabicName = "محمد عبدالله أحمد الزهراني")
    {
        var interestId = await SeedInterestAsync();
        // B3 — D-221: organisation is now required, so the baseline valid
        // request carries one. Bad-org tests override OrganisationId afterwards.
        var organisationId = await SeedOrganisationAsync();
        return new UpsertUserProfileRequest
        {
            InterestIds = new List<Guid> { interestId },
            ArabicName = arabicName,
            EnglishName = englishName,
            NationalityCode = "SA",
            DateOfBirth = new DateOnly(1990, 1, 1),
            PlaceOfBirth = "Riyadh",
            IsSaudi = true,
            // H-1 (FIX A) — the self-service write path now populates the National-ID
            // blind index and dedups on it, and this class shares ONE DB across tests,
            // so a hardcoded id would 409 the second distinct user. Mint a UNIQUE
            // Luhn-valid Saudi id per call (mirrors WalkInRegistrationTests).
            NationalId = TestIdentity.MintNationalId(),
            OrganisationId = organisationId,
        };
    }

    /// <summary>Creates one active <see cref="UserInterest"/> directly via the
    /// DbContext (the admin endpoint path is exercised in
    /// <see cref="InterestTests"/>). Returns the id so the test can pin
    /// it into the upsert request — the P9 validator requires 1-10.</summary>
    private async Task<Guid> SeedInterestAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var interest = new UserInterest
        {
            Id = Guid.NewGuid(),
            Name = $"Test Interest {Guid.NewGuid():N}",
            NameArabic = "اهتمام اختبار",
            DisplayOrder = 0,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.Interests.Add(interest);
        await db.SaveChangesAsync();
        await appDb.SaveChangesAsync();
        return interest.Id;
    }

    private async Task<string> CreateUserAndSignInAsync(bool withIdImage = true)
    {
        var email = $"up-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Profile Test",
                AccountState = AccountState.Approved,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;
            // D-106: QR id lives on UserProfile now. Seed a stub profile row
            // with a pre-baked QR so the GET-profile tests still surface one
            // before the visitor fills the form.
            var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            appDb.UserProfiles.Add(new SIMF.Domain.Profiles.UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                QrId = $"TEST{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            await appDb.SaveChangesAsync();
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        var token = body.Data!.Tokens!.AccessToken;
        // The two-photo split makes the ID document mandatory for EVERY upsert,
        // so the happy-path tests start with one already uploaded; the few tests
        // that assert the "no image" surface pass withIdImage: false.
        if (withIdImage)
        {
            await UploadValidIdImageAsync(token);
        }
        return token;
    }

    private async Task<Guid> GetActorIdAsync(string token)
    {
        // Parse the JWT's sub claim — much simpler than another DB roundtrip.
        var middle = token.Split('.')[1];
        var padded = middle.PadRight(middle.Length + (4 - middle.Length % 4) % 4, '=');
        var json = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/')));
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
    }

    // D-609 — an APPROVED visitor whose DisplayName is either the email placeholder
    // (displayName == null → seeded to Email, as RegistrationService does) or an
    // explicit admin-set name. Uploads the ID document + an avatar so the profile
    // upsert clears both the ID and the male-photo gate regardless of gender.
    private async Task<(string Token, Guid UserId, string Email)> CreateApprovedVisitorAsync(
        string? displayName)
    {
        var email = $"up-rename-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName ?? email,
                AccountState = AccountState.Approved,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;
            var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
            appDb.UserProfiles.Add(new SIMF.Domain.Profiles.UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                QrId = $"TEST{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await appDb.SaveChangesAsync();
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var token = (await sign.Content
            .ReadFromJsonAsync<ApiResult<SignInResponse>>())!.Data!.Tokens!.AccessToken;
        await UploadValidIdImageAsync(token);
        await UploadValidAvatarAsync(token);
        return (token, userId, email);
    }

    private async Task<string?> GetDisplayNameAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = await users.FindByIdAsync(userId.ToString());
        return user?.DisplayName;
    }

    private async Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private static int IndexOfSequence(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length) return -1;
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }
}
