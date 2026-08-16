// The append-only pin for the app-facing wire contract.
//
// The shipped Flutter app decodes JSON by KEY NAME (`json['nameArabic']`,
// `json['accessToken']`, `json['success']` — see
// src/Mobile/simf_app/packages/simf_data_pkg/lib/src/api/api_result.dart and the
// per-feature `*_models.dart` files). A published key can therefore never be
// removed or renamed: the app in the field is not rebuilt when the API is, so a
// rename compiles, deploys, and then silently returns null to every install.
// Adding a key is always safe — the app ignores what it does not read.
//
// Until this file existed that rule lived only in prose and in a reviewer's
// manual grep. This test is the enforcement: it walks every response contract
// reachable from a `/app/*` endpoint and fails the build when a key that was
// once published stops being emitted.
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using SIMF.Api.Serialization;
using SIMF.Common;
using SIMF.Contracts.Account;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Ai;
using SIMF.Contracts.Archive;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Cms;
using SIMF.Contracts.Configuration;
using SIMF.Contracts.Contacts;
using SIMF.Contracts.Delegations;
using SIMF.Contracts.Exhibition;
using SIMF.Contracts.Exhibitors;
using SIMF.Contracts.Faq;
using SIMF.Contracts.Feedback;
using SIMF.Contracts.Gates;
using SIMF.Contracts.Media;
using SIMF.Contracts.Networking;
using SIMF.Contracts.Notifications;
using SIMF.Contracts.Organisations;
using SIMF.Contracts.Organization;
using SIMF.Contracts.Programme;
using SIMF.Contracts.PublicRelations;
using SIMF.Contracts.Recommendations;
using SIMF.Contracts.Regions;
using SIMF.Contracts.Requests;
using SIMF.Contracts.Sessions;
using SIMF.Contracts.Sponsors;
using SIMF.Contracts.UserProfile;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class AppWireContractPinTests
{
    /// <summary>
    /// The options the API actually serializes responses with.
    ///
    /// <para>Program.cs configures FastEndpoints with
    /// <c>config.Serializer.Options</c> and adds exactly one converter, the Saudi
    /// local-time <see cref="SaudiDateTimeOffsetJsonConverter"/>; nothing in
    /// SIMF.Api sets a naming policy, so the names are FastEndpoints' System.Text.Json
    /// "Web" defaults (camelCase). The same conclusion is written down twice more in
    /// the codebase: <c>ErrorHandlingMiddleware</c> writes the production error
    /// envelope with <c>new JsonSerializerOptions(JsonSerializerDefaults.Web)</c>,
    /// and the shipped app reads camelCase keys off the wire.</para>
    /// </summary>
    private static readonly JsonSerializerOptions WireOptions = BuildWireOptions();

    /// <summary>
    /// Every response payload returned by a <c>/app/*</c> endpoint, read off the
    /// endpoint declarations in <c>src/Backend/SIMF.Api/Endpoints</c> (the payload
    /// unwrapped from <c>ApiResult&lt;T&gt;</c>, <c>IReadOnlyList&lt;T&gt;</c> and
    /// <c>GridPage&lt;T&gt;</c>). Routes returning <c>bool</c>, a bare
    /// <c>Guid</c>, or a file/byte stream carry no contract and are absent.
    ///
    /// <para>Nested types are NOT listed: the walk follows every property, so a
    /// contract's children are pinned automatically.</para>
    /// </summary>
    private static readonly Type[] AppResponseContracts =
    [
        // The envelope itself. The app parses `success` / `data` / `error` before it
        // parses any payload, so the envelope is as append-only as the payloads are.
        typeof(ApiResult<object>),
        typeof(GridPage<NotificationDto>),          // POST /app/account/notifications/list

        typeof(AvatarResponse),                     // DELETE /app/account/avatar
        typeof(ConnectionRow),                      // GET    /app/account/connections
        typeof(ConnectionResult),                   // POST   /app/account/connections
        typeof(MyAreaDashboard),                    // GET    /app/account/dashboard
        typeof(InterestListResponse),               // GET    /app/account/interests
        typeof(UnreadCountResponse),                // GET    /app/account/notifications/unread-count
        typeof(AccountPreferences),                 // GET    /app/account/preferences
        typeof(ProfileResponse),                    // GET    /app/account/profile
        typeof(ProfileTypePickerListResponse),      // GET    /app/account/profile-types
        typeof(RecommendationsResponse),            // GET    /app/account/recommendations/meet-like-you
        typeof(RecoveryCodesResponse),              // POST   /app/account/recovery-codes/regenerate
        typeof(MyAreaSessions),                     // GET    /app/account/sessions
        typeof(VisitorShareTokenResponse),          // GET    /app/account/share-token
        typeof(UserProfileResponse),                // GET    /app/account/user-profile
        typeof(CountryListResponse),                // GET    /app/account/user-profile/countries
        typeof(AiCallResult),                       // POST   /app/ai/assistance
        typeof(AiChatTurn),                         // GET    /app/ai/assistance/history
        typeof(PublicArchive),                      // GET    /app/archive
        typeof(PublicArchiveEditionDetail),         // GET    /app/archive/{id}
        typeof(ArchiveVisibilityState),             // GET    /app/archive/visibility
        typeof(BadgeActivationStartResponse),       // POST   /app/auth/badge-activation/start
        typeof(BadgeActivationCompleteResponse),    // POST   /app/auth/badge-activation/complete
        typeof(SignInResponse),                     // POST   /app/auth/sign-in, /app/auth/badge-sign-in
        typeof(ChangePasswordResponse),             // POST   /app/auth/change-password
        typeof(CompletePasswordChangeResponse),     // POST   /app/auth/complete-password-change
        typeof(DeviceKeyEntry),                     // GET    /app/auth/device-keys
        typeof(DeviceKeyChallenge),                 // POST   /app/auth/device-keys/{id}/challenge
        typeof(SendBiometricStepUpResponse),        // POST   /app/auth/device-keys/step-up
        typeof(ForgotPasswordResponse),             // POST   /app/auth/forgot-password
        typeof(AuthTokens),                         // POST   /app/auth/refresh, /app/auth/verify-otp
        typeof(ResendCodeResponse),                 // POST   /app/auth/resend-code
        typeof(ResendOtpResponse),                  // POST   /app/auth/resend-otp
        typeof(ResetPasswordResponse),              // POST   /app/auth/reset-password
        typeof(ResolveBadgeResponse),               // POST   /app/auth/resolve-badge
        typeof(SignOutResponse),                    // POST   /app/auth/sign-out
        typeof(SignUpResponse),                     // POST   /app/auth/sign-up
        typeof(TotpSetupResponse),                  // GET    /app/auth/totp/pairing
        typeof(TotpConfirmResponse),                // POST   /app/auth/totp/confirm
        typeof(TotpDisableResponse),                // POST   /app/auth/totp/disable
        typeof(CompleteTwoFactorEnrolmentResponse), // POST   /app/auth/totp/enrolment/complete
        typeof(TotpPairingVerifyResponse),          // POST   /app/auth/totp/pairing/verify
        typeof(VerifyEmailResponse),                // POST   /app/auth/verify-email
        typeof(BadgeUpdateRequestSubmitted),        // POST   /app/badge-requests
        typeof(PublicBanners),                      // GET    /app/banners
        typeof(PublicBoothSummary),                 // GET    /app/booths
        typeof(PublicBoothDetail),                  // GET    /app/booths/{id}
        typeof(AppBootstrap),                       // GET    /app/bootstrap
        typeof(SavedContactRow),                    // GET    /app/contacts, POST /app/contacts/save
        typeof(VisitorCard),                        // POST   /app/contacts/resolve
        typeof(PublicContentBlock),                 // GET    /app/content/{key}
        typeof(PublicContentBlockBatch),            // POST   /app/content/batch
        typeof(DelegationAvailableSlot),            // GET    /app/countries/{countryId}/available-slots
        typeof(AppDelegations),                     // GET    /app/delegations
        typeof(DelegationMeetingRequestSubmitted),  // POST   /app/delegation-meeting-requests
        typeof(AdminDelegationMeetingRequestDetail),// POST   /app/delegation-meeting-requests/{id}/confirm
        typeof(ParticipationDocumentRequestSubmitted), // POST /app/document-requests
        typeof(ExhibitorVisitorRow),                // GET    /app/exhibitor/visitors
        typeof(PublicFaqGroup),                     // GET    /app/faq
        typeof(RatingFormView),                     // GET    /app/feedback/form
        typeof(RatingSubmissionView),               // POST   /app/feedback/submit
        typeof(GateScanResponse),                   // POST   /app/gates/{gateId}/scans
        typeof(GateVisitorsListResponse),           // POST   /app/gates/{gateId}/visitors/list
        typeof(OperatorGateAssignment),             // GET    /app/gates/my-assignments
        typeof(OperatorDailyReport),                // GET    /app/gates/my-reports/today
        typeof(GateOfflineConfig),                  // GET    /app/gates/offline-config
        typeof(GateOfflineRoster),                  // GET    /app/gates/offline-roster
        typeof(PublicMediaPage),                    // GET    /app/media
        typeof(PublicMediaPartners),                // GET    /app/media-partners
        typeof(MeetingActionPreview),               // GET    /app/meeting-actions/{token}
        typeof(MeetingActionOutcome),               // POST   /app/meeting-actions/{token}
        typeof(RecordDevicePositionsResponse),      // POST   /app/movement/pings
        typeof(AppRequestItem),                     // GET    /app/my-requests
        typeof(PartnerDirectoryResponse),           // GET    /app/networking/partner-directory
        typeof(PublicNewsPage),                     // GET    /app/news
        typeof(PublicNewsArticle),                  // GET    /app/news/{id}
        typeof(OrganisationPickerItem),             // GET    /app/organisations
        typeof(OrganizationProfileResponse),        // GET    /app/organization-profile
        typeof(PublicPresentations),                // GET    /app/presentations
        typeof(PublicProgrammeDays),                // GET    /app/programme/days
        typeof(PublicSessions),                     // GET    /app/programme/sessions
        typeof(PublicSessionDetail),                // GET    /app/programme/sessions/{id}
        typeof(PublicRecordedQuestion),             // GET    /app/programme/sessions/{id}/recorded-questions
        typeof(RecordingStreamTokenResponse),       // POST   /app/programme/sessions/{id}/recording/token
        typeof(PublicSessionSummary),               // GET    /app/programme/sessions/{id}/summary
        typeof(HostSessionSummary),                 // GET    /app/programme/sessions/{id}/summary/approved
        typeof(RegionPickerItem),                   // GET    /app/regions
        typeof(HallAttendanceStatus),               // GET    /app/sessions/{sessionId}/attendance
        typeof(SessionQuestionSubmitted),           // POST   /app/sessions/{sessionId}/questions
        typeof(SessionQuestionModeratorRow),        // GET    /app/sessions/{sessionId}/questions/moderate
        typeof(SessionSeatMap),                     // GET    /app/sessions/{sessionId}/seats
        typeof(MySeatReservation),                  // POST   /app/sessions/{sessionId}/seats/reserve
        typeof(ModeratedSessionRow),                // GET    /app/sessions/moderated
        typeof(SiteSettingsResponse),               // GET    /app/site-settings
        typeof(PublicSpeakers),                     // GET    /app/speakers
        typeof(PublicSpeakerDetail),                // GET    /app/speakers/{id}
        typeof(SpeakerAvailableSlot),               // GET    /app/speakers/{speakerId}/available-slots
        typeof(SpeakerMeetingRequestSubmitted),     // POST   /app/speakers/{speakerId}/meeting-requests
        typeof(PublicSponsors),                     // GET    /app/sponsors
        typeof(PublicSponsorDetail),                // GET    /app/sponsors/{id}
        typeof(StaffSeatOccupant),                  // GET    /app/staff/sessions/{sessionId}/seating/seat
        typeof(AdminWalkInRegistrationResponse),    // POST   /app/staff/visitors/register-onsite
        typeof(CurrentUserResponse),                // GET    /app/users/me
        typeof(PublicVenueMapNode),                 // GET    /app/venue-map
        typeof(AppVersionPolicyResponse),           // GET    /app/version-policy
    ];

    /// <summary>
    /// The published wire, one line per contract: <c>Full.Type.Name: key,key,key</c>,
    /// keys sorted ordinally.
    ///
    /// <para><b>Adding</b> a property to a contract needs no edit here — the check is
    /// "every key on this list is still emitted", not "the two sets are equal", which
    /// is exactly what append-only means. <b>Removing</b> a line, or a key from a
    /// line, is a deliberate act that says "no shipped app build reads this any more".
    /// A contract that is reachable but has no line at all fails too, with the line to
    /// paste, so a new contract cannot quietly escape the pin.</para>
    /// </summary>
    private static readonly string[] PublishedWire =
    [
        "SIMF.Common.ApiError: code,details,message,messageArabic",
        "SIMF.Common.ApiErrorDetail: field,message,messageArabic",
        "SIMF.Common.ApiResult<System.Object>: data,error,meta,success",
        "SIMF.Common.GridPage<SIMF.Contracts.Notifications.NotificationDto>: items,skip,top,total",
        "SIMF.Contracts.Account.AccountPreferences: captions,configured,highContrast,reduceMotion,screenReaderAssist,textSize",
        "SIMF.Contracts.Account.AppBootstrap: serverTime,unreadNotificationCount,user",
        "SIMF.Contracts.Account.MyAreaCounters: bookedSessionsCount,meetingsCount",
        "SIMF.Contracts.Account.MyAreaDashboard: counters,identity,todaySchedule",
        "SIMF.Contracts.Account.MyAreaIdentity: avatarUrl,fullNameAr,fullNameEn,isVisitor,pageColor,qrId,tierNameAr,tierNameEn",
        "SIMF.Contracts.Account.MyAreaScheduleItem: end,hallNameAr,hallNameEn,kind,meetingId,sessionId,start,status,subject,titleAr,titleEn",
        "SIMF.Contracts.Account.MyAreaSessionItem: attended,categoryNameAr,categoryNameEn,end,hallNameAr,hallNameEn,id,isFavourite,speakerNameAr,speakerNameEn,speakerTitle,start,status,title,titleArabic",
        "SIMF.Contracts.Account.MyAreaSessions: items",
        "SIMF.Contracts.Admin.ArchiveVisibilityState: isVisible,lastChangedAt,lastChangedByUserId",
        "SIMF.Contracts.Ai.AiCallResult: feature,invocationId,isStub,latencyMs,model,outputText,promptKey,provider,tokensInput,tokensOutput",
        "SIMF.Contracts.Ai.AiChatTurn: content,role",
        "SIMF.Contracts.Archive.PublicArchive: items",
        "SIMF.Contracts.Archive.PublicArchiveEdition: attendees,coverImageRelativePath,dateLabelAr,dateLabelEn,hasCoverAsset,id,locationAr,locationEn,sessions,speakers,summaryAr,summaryEn,titleAr,titleEn,year",
        "SIMF.Contracts.Archive.PublicArchiveEditionDetail: attendees,coverImageRelativePath,dateLabelAr,dateLabelEn,gallery,id,locationAr,locationEn,pastSpeakers,sessionTitles,sessions,speakers,summaryAr,summaryEn,titleAr,titleEn,year",
        "SIMF.Contracts.Archive.PublicArchiveMediaItem: captionAr,captionEn,kind,url",
        "SIMF.Contracts.Archive.PublicArchivePastSpeaker: countryId,nameAr,nameEn,photoRelativePath",
        "SIMF.Contracts.Archive.PublicArchiveSessionTitle: titleAr,titleEn",
        "SIMF.Contracts.Authentication.AccountStateInfo: rejectionReason,rejectionReasonArabic,state,stateChangedAt",
        "SIMF.Contracts.Authentication.AdminWalkInRegistrationResponse: displayName,email,profileTypeColor,profileTypeName,profileTypeNameArabic,qrId,userId,userProfileId",
        "SIMF.Contracts.Authentication.AuthTokens: accessToken,accessTokenExpiresInSeconds,previousSignInAt,refreshToken,tokenType,user",
        "SIMF.Contracts.Authentication.AuthUser: displayName,email,id",
        "SIMF.Contracts.Authentication.AvatarResponse: avatarUrl",
        "SIMF.Contracts.Authentication.BadgeActivationCompleteResponse: activated",
        "SIMF.Contracts.Authentication.BadgeActivationStartResponse: codeExpiresInSeconds,maskedEmail",
        "SIMF.Contracts.Authentication.ChangePasswordResponse: passwordChanged",
        "SIMF.Contracts.Authentication.CompletePasswordChangeResponse: passwordChanged",
        "SIMF.Contracts.Authentication.CompleteTwoFactorEnrolmentResponse: recoveryCodes,tokens",
        "SIMF.Contracts.Authentication.CurrentUserResponse: appRole,avatarUrl,displayName,email,id,preferredLanguage,profileComplete,registrationStatus",
        "SIMF.Contracts.Authentication.DeviceKeyChallenge: challenge,expiresInSeconds",
        "SIMF.Contracts.Authentication.DeviceKeyEntry: algorithm,createdAt,id,label,lastUsedAt,revokedAt,userId",
        "SIMF.Contracts.Authentication.ForgotPasswordResponse: codeExpiresInSeconds",
        "SIMF.Contracts.Authentication.ProfileResponse: avatarUrl,displayName,email,id,recoveryCodesRemaining,roles,twoFactorEnabled",
        "SIMF.Contracts.Authentication.RecoveryCodesResponse: recoveryCodes",
        "SIMF.Contracts.Authentication.ResendCodeResponse: codeExpiresInSeconds,email",
        "SIMF.Contracts.Authentication.ResendOtpResponse: cooldownSeconds",
        "SIMF.Contracts.Authentication.ResetPasswordResponse: passwordReset",
        "SIMF.Contracts.Authentication.ResolveBadgeResponse: displayName,found,hasPassword,maskedEmail,needsEmail",
        "SIMF.Contracts.Authentication.SendBiometricStepUpResponse: expiresInSeconds,maskedEmail",
        "SIMF.Contracts.Authentication.SignInResponse: accountState,mfaRequired,mfaToken,otpToken,passwordChangeToken,tokens,twoFactorEnrolmentToken",
        "SIMF.Contracts.Authentication.SignOutResponse: signedOut",
        "SIMF.Contracts.Authentication.SignUpResponse: codeExpiresInSeconds,email",
        "SIMF.Contracts.Authentication.TotpConfirmResponse: recoveryCodes,twoFactorEnabled",
        "SIMF.Contracts.Authentication.TotpDisableResponse: twoFactorEnabled",
        "SIMF.Contracts.Authentication.TotpPairingVerifyResponse: valid",
        "SIMF.Contracts.Authentication.TotpSetupResponse: otpAuthUri,qrCodeSvg,secret",
        "SIMF.Contracts.Authentication.VerifyEmailResponse: email,emailVerified",
        "SIMF.Contracts.Cms.PublicBanner: body,bodyArabic,displayOrder,end,id,imageUrl,linkUrl,start,title,titleArabic",
        "SIMF.Contracts.Cms.PublicBanners: items",
        "SIMF.Contracts.Cms.PublicContentBlock: content,contentArabic,key,lastUpdatedAt",
        "SIMF.Contracts.Cms.PublicContentBlockBatch: blocks",
        "SIMF.Contracts.Configuration.AppVersionPolicyResponse: android,ios",
        "SIMF.Contracts.Configuration.PlatformVersionPolicy: latestVersion,minVersion,storeUrl",
        "SIMF.Contracts.Configuration.SiteSettingsResponse: partnerDirectoryEnabled,registrationSuccessMessageAr,registrationSuccessMessageEn,sessionRatingEnabled,social",
        "SIMF.Contracts.Contacts.SavedContactRow: id,jobTitle,jobTitleArabic,name,nameArabic,note,organisation,savedAt,subjectAvailable,subjectUserId",
        "SIMF.Contracts.Contacts.VisitorCard: available,countryId,countryName,countryNameArabic,email,internationalMobile,jobTitle,jobTitleArabic,name,nameArabic,organisation,organisationArabic,saudiMobile,userId,userProfileId",
        "SIMF.Contracts.Contacts.VisitorShareTokenResponse: token",
        "SIMF.Contracts.Delegations.AppDelegationItem: arrivalDate,countryCode,countryId,countryName,countryNameArabic,departureDate,headName,headNameArabic,headTitle,headTitleArabic,memberCount",
        "SIMF.Contracts.Delegations.AppDelegations: countryCount,items,totalParticipants",
        "SIMF.Contracts.Exhibition.PublicBoothDetail: city,cityArabic,code,countryId,countryName,countryNameArabic,description,descriptionArabic,exhibitorContactId,exhibitorId,exhibitorName,exhibitorNameArabic,hallId,hallName,hallNameArabic,id,mapX,mapY,name,nameArabic,officerEmail,officerName,officerPhone,sector,sectorArabic,tier,tierName,website",
        "SIMF.Contracts.Exhibition.PublicBoothSummary: code,countryId,countryName,countryNameArabic,exhibitorContactId,exhibitorName,exhibitorNameArabic,hallId,hallName,hallNameArabic,id,mapX,mapY,name,nameArabic,officerEmail,officerName,officerPhone,sector,sectorArabic",
        "SIMF.Contracts.Exhibitors.ExhibitorVisitorRow: card,id,note,scannedAt",
        "SIMF.Contracts.Faq.PublicFaqEntry: answer,answerArabic,id,question,questionArabic",
        "SIMF.Contracts.Faq.PublicFaqGroup: entries,id,name,nameArabic",
        "SIMF.Contracts.Feedback.RatingAnswerInput: questionId,stars",
        "SIMF.Contracts.Feedback.RatingExistingAnswer: questionId,stars",
        "SIMF.Contracts.Feedback.RatingExistingSubmission: answers,comment,overallStars",
        "SIMF.Contracts.Feedback.RatingFormGroup: displayOrder,id,name,nameArabic,questions",
        "SIMF.Contracts.Feedback.RatingFormQuestion: displayOrder,id,isRequired,text,textArabic",
        "SIMF.Contracts.Feedback.RatingFormView: allowComment,code,commentLabel,commentLabelArabic,existing,groups,hasOverallStars,isEligible,name,nameArabic,ratingTypeId,scope,targetId,targetName,targetNameArabic,targetStart,ungroupedQuestions",
        "SIMF.Contracts.Feedback.RatingSubmissionView: answers,comment,createdAt,id,overallStars,ratingTypeId,targetId,updatedAt",
        "SIMF.Contracts.Gates.GateOfflineConfig: badgeKey,badgeKeyVersion,gates,issuedAt,previousBadgeKey,previousBadgeKeyVersion,sessionWalkIn",
        "SIMF.Contracts.Gates.GateOfflineRoster: attendees,issuedAt,validUntil",
        "SIMF.Contracts.Gates.GateOfflineRosterEntry: hallId,isAdmitted,name,nameArabic,profileTypeCode,rowLabel,seatNumber,sessionEnd,sessionId,sessionStart,userProfileId",
        "SIMF.Contracts.Gates.GateOfflineRule: allowedProfileTypeCodes,code,gateId,isActive,isHallDoor",
        "SIMF.Contracts.Gates.GateScanResponse: denialMessage,denialReasonCode,direction,noticeMessage,outcome,scanId,scannedAt,userProfile",
        "SIMF.Contracts.Gates.GateScanUserProfile: displayName,displayNameArabic,id,profileTypeId,profileTypeName,profileTypePageColor",
        "SIMF.Contracts.Gates.GateVisitorListItem: denialReasonCode,direction,displayName,outcome,profileTypeName,qrIdAtScan,scanId,scannedAt,userProfileId",
        "SIMF.Contracts.Gates.GateVisitorsListResponse: asOf,items,nextCursor",
        "SIMF.Contracts.Gates.OperatorDailyReport: denialBreakdown,fromUtc,operatorUserId,rows,toUtc,totals",
        "SIMF.Contracts.Gates.OperatorDailyReportTotals: allowed,denied",
        "SIMF.Contracts.Gates.OperatorDenialBucket: code,count",
        "SIMF.Contracts.Gates.OperatorGateAssignment: code,directionMode,gateId,isActive,name,nameArabic",
        "SIMF.Contracts.Gates.OperatorScanRow: denialReasonCode,direction,outcome,scanId,scannedAt,visitorDisplayName",
        "SIMF.Contracts.Media.PublicMediaItem: album,albumArabic,displayOrder,id,imageUrl,kind,thumbnailUrl,title,titleArabic,videoUrl",
        "SIMF.Contracts.Media.PublicMediaPage: items,skip,top,total",
        "SIMF.Contracts.Networking.ConnectionResult: createdAt,id,requesterUserId,state,targetUserId",
        "SIMF.Contracts.Networking.ConnectionRow: createdAt,id,isIncoming,otherDisplayName,otherUserId,state",
        "SIMF.Contracts.Networking.PartnerDirectoryEntry: countryId,countryNameAr,countryNameEn,id,kind,logoContactId,logoRelativePath,name,nameArabic,subtitle,subtitleArabic",
        "SIMF.Contracts.Networking.PartnerDirectoryResponse: entries",
        "SIMF.Contracts.Notifications.NotificationDto: body,bodyArabic,clickUrl,createdAt,group,id,isRead,kind,readAt,relatedEntityId,relatedEntityType,severity,title,titleArabic",
        "SIMF.Contracts.Notifications.UnreadCountResponse: unreadCount",
        "SIMF.Contracts.Organisations.OrganisationPickerItem: city,id,nameAr,nameEn",
        "SIMF.Contracts.Organization.OrganizationAboutItemDto: displayOrder,id,text,textArabic,title,titleArabic",
        "SIMF.Contracts.Organization.OrganizationDetailDto: displayOrder,id,name,nameArabic,value,valueArabic",
        "SIMF.Contracts.Organization.OrganizationProfileResponse: aboutItems,backgroundVideoUrl,bio,bioArabic,contactEmail,contactPhone,contactWebsite,currentYear,details,eventEndDate,eventStartDate,latitude,liveStreamUrl,locationText,locationTextArabic,logoUrl,longitude,name,nameArabic,releaseDate,slogan,sloganArabic,social,status,sysVersion,title,titleArabic,version,versionDate",
        "SIMF.Contracts.Programme.AdminDelegationMeetingRequestDetail: attendeeCount,createdAt,id,requestedByUserId,requesterEmail,requestingCountry,respondedAt,responseNote,slotEnd,slotStart,status,subject,targetCountry",
        "SIMF.Contracts.Programme.DelegationAvailableSlot: end,start",
        "SIMF.Contracts.Programme.DelegationMeetingRequestSubmitted: createdAt,id,status",
        "SIMF.Contracts.Programme.HostSessionSummary: approvedAt,fullText,fullTextArabic,generatedByAi,keyPoints,keyPointsArabic,recommendations,recommendationsArabic,sessionId,speakers,speakersArabic",
        "SIMF.Contracts.Programme.MeetingActionOutcome: action",
        "SIMF.Contracts.Programme.MeetingActionPreview: action,hallName,requesterName,slotEnd,slotStart,speakerName,speakerNameArabic,subject",
        "SIMF.Contracts.Programme.PublicPresentationItem: contentType,fileName,id,sessionId,sessionStart,sessionTitle,sessionTitleArabic,sizeBytes,speakerName,speakerNameArabic",
        "SIMF.Contracts.Programme.PublicPresentations: items",
        "SIMF.Contracts.Programme.PublicProgrammeDay: date,displayOrder,hasImage,id,sessions,title,titleArabic",
        "SIMF.Contracts.Programme.PublicProgrammeDays: days",
        "SIMF.Contracts.Programme.PublicRecordedQuestion: askedByDisplayName,createdAt,id,isPushed,questionText,recipient",
        "SIMF.Contracts.Programme.PublicSessionDetail: arrivalGraceMinutes,categoryId,categoryName,categoryNameArabic,code,description,descriptionArabic,displayOrder,downloads,end,hallId,hallName,hallNameArabic,hasRecording,id,language,languageArabic,liveCaptions,liveCaptionsArabic,liveNotice,liveNoticeArabic,liveSignLanguageUrl,liveStreamUrl,outcomes,publishedAt,seats,speakers,start,status,themes,title,titleArabic,type",
        "SIMF.Contracts.Programme.PublicSessionDownload: contentType,fileName,id,sizeBytes",
        "SIMF.Contracts.Programme.PublicSessionListItem: categoryId,categoryName,categoryNameArabic,code,description,descriptionArabic,end,hallId,hallName,hallNameArabic,hasPublishedSummary,id,primaryThemeColor,primaryThemeName,primaryThemeNameArabic,speakers,start,status,title,titleArabic,type",
        "SIMF.Contracts.Programme.PublicSessionOutcome: text,textArabic",
        "SIMF.Contracts.Programme.PublicSessionSeatSummary: available,capacity,reserved",
        "SIMF.Contracts.Programme.PublicSessionSpeaker: countryId,countryNameAr,countryNameEn,displayOrder,hasPhotoAsset,id,name,nameArabic,photoRelativePath,role,title,titleArabic",
        "SIMF.Contracts.Programme.PublicSessionSummary: fullText,fullTextArabic,generatedByAi,keyPoints,keyPointsArabic,publishedAt,recommendations,recommendationsArabic,recordingUrl,sessionId,speakers,speakersArabic,summaryVideoUrl",
        "SIMF.Contracts.Programme.PublicSessionTheme: color,description,descriptionArabic,id,name,nameArabic",
        "SIMF.Contracts.Programme.PublicSessions: items",
        "SIMF.Contracts.Programme.PublicSpeakerDetail: allowsDataSharing,allowsMeetingRequests,awards,awardsArabic,bio,bioArabic,countryId,countryNameAr,countryNameEn,displayOrder,facebookUrl,id,linkedInUrl,name,nameArabic,photoRelativePath,qualifications,qualificationsArabic,rank,rankArabic,sessions,trainingExperience,trainingExperienceArabic,websiteUrl,xUrl",
        "SIMF.Contracts.Programme.PublicSpeakerSession: code,end,hallId,hallName,hallNameArabic,id,start,title,titleArabic",
        "SIMF.Contracts.Programme.PublicSpeakerSummary: countryId,countryNameAr,countryNameEn,displayOrder,hasPhotoAsset,id,name,nameArabic,photoRelativePath,rank,rankArabic",
        "SIMF.Contracts.Programme.PublicSpeakers: items",
        "SIMF.Contracts.Programme.PublicVenueMapNode: boothId,hallId,id,kind,label,labelArabic,x,y",
        "SIMF.Contracts.Programme.RecordDevicePositionsResponse: accepted,matchedToHall",
        "SIMF.Contracts.Programme.RecordingStreamTokenResponse: expiresInSeconds,streamUrl,token",
        "SIMF.Contracts.Programme.SpeakerAvailableSlot: end,start",
        "SIMF.Contracts.Programme.SpeakerMeetingRequestSubmitted: createdAt,id,speakerId,status",
        "SIMF.Contracts.PublicRelations.PublicMediaPartnerItem: displayOrder,email,facebookUrl,id,instagramUrl,latitude,linkedInUrl,logoRelativePath,longitude,name,nameArabic,phonePrimary,url,xUrl",
        "SIMF.Contracts.PublicRelations.PublicMediaPartners: items",
        "SIMF.Contracts.PublicRelations.PublicNewsArticle: body,bodyArabic,category,categoryArabic,id,imageRelativePath,publishedAt,title,titleArabic",
        "SIMF.Contracts.PublicRelations.PublicNewsListItem: category,categoryArabic,excerpt,excerptArabic,id,imageRelativePath,publishedAt,title,titleArabic",
        "SIMF.Contracts.PublicRelations.PublicNewsPage: items,page,pageSize,total",
        "SIMF.Contracts.Recommendations.MatchedInterest: id,name,nameArabic",
        "SIMF.Contracts.Recommendations.RecommendationEntry: arabicName,englishName,jobTitle,matchReason,matchReasonArabic,profileTypeName,profileTypeNameArabic,score,sharedInterestCount,sharedInterests,sharedSessionCount,userProfileId",
        "SIMF.Contracts.Recommendations.RecommendationsResponse: matches",
        "SIMF.Contracts.Regions.RegionPickerItem: code,name,nameArabic",
        "SIMF.Contracts.Requests.AppRequestItem: canCancel,checkedIn,countryId,createdAt,eventDate,id,kind,responseNote,speakerId,status,subtitle,subtitleArabic,title,titleArabic",
        "SIMF.Contracts.Requests.BadgeUpdateRequestSubmitted: createdAt,id,status",
        "SIMF.Contracts.Requests.ParticipationDocumentRequestSubmitted: createdAt,documentType,id,status",
        "SIMF.Contracts.Sessions.HallAttendanceStatus: arrived,enter,leave,method",
        "SIMF.Contracts.Sessions.ModeratedSessionRow: assignedAt,end,hallName,hallNameArabic,sessionId,start,title,titleArabic",
        "SIMF.Contracts.Sessions.MySeatReservation: createdAt,kind,reservationId,rowLabel,seatNumber,sessionId,status",
        "SIMF.Contracts.Sessions.SessionQuestionModeratorRow: createdAt,id,isHidden,isPushed,order,phase,pushedAt,questionText,recipient,sessionId,status,submittedByDisplayName,submittedByEmail,submittedByUserId",
        "SIMF.Contracts.Sessions.SessionQuestionSubmitted: createdAt,id,order,sessionId",
        "SIMF.Contracts.Sessions.SessionSeatCell: checkedIn,guestHint,guestHintArabic,kind,reservationId,rowLabel,seatNumber,status",
        "SIMF.Contracts.Sessions.SessionSeatMap: activeReservedCount,callerIsVip,hallCapacity,hallId,mode,myCell,reservedCells,rowLabels,seatCounts,seatTiers,seatsPerRow,sessionCapacity,sessionId,sessionTitle,sessionTitleArabic",
        "SIMF.Contracts.Sessions.StaffSeatOccupant: checkedIn,displayName,displayNameArabic,found,guestHint,guestHintArabic,hasPhoto,kind,qrId,reservationId,rowLabel,seatNumber,sessionId,status,tier,userId,userProfileId",
        "SIMF.Contracts.SocialLinks: facebook,instagram,linkedIn,snapchat,tikTok,x,youTube",
        "SIMF.Contracts.Sponsors.PublicSponsor: countryId,countryNameAr,countryNameEn,displayOrder,email,facebookUrl,hasLogo,id,instagramUrl,latitude,linkedInUrl,logoRelativePath,longitude,nameAr,nameEn,phonePrimary,tagline,taglineArabic,tier,tierName,url,xUrl",
        "SIMF.Contracts.Sponsors.PublicSponsorDetail: about,aboutArabic,city,cityArabic,countryId,countryNameAr,countryNameEn,id,logoRelativePath,nameAr,nameEn,tier,tierName,url",
        "SIMF.Contracts.Sponsors.PublicSponsorTierGroup: sponsors,tier,tierName",
        "SIMF.Contracts.Sponsors.PublicSponsors: groups",
        "SIMF.Contracts.UserProfile.CountryDto: code,name,nameArabic",
        "SIMF.Contracts.UserProfile.CountryListResponse: countries",
        "SIMF.Contracts.UserProfile.InterestDto: displayOrder,id,name,nameArabic",
        "SIMF.Contracts.UserProfile.InterestListResponse: interests",
        "SIMF.Contracts.UserProfile.ProfileTypePickerDto: id,isVisitor,name,nameArabic,pageColor",
        "SIMF.Contracts.UserProfile.ProfileTypePickerListResponse: items",
        "SIMF.Contracts.UserProfile.UserProfileResponse: allowsDelegationMeeting,allowsSpeakerMeeting,arabicName,dateOfBirth,englishName,gender,hasAvatar,hasIdImage,interestIds,internationalMobile,iqamaNumber,isForVisitor,isSaudi,isVip,jobTitle,jobTitleArabic,nationalId,nationalityCode,organisationId,passportNumber,placeOfBirth,plateNumber,plateNumberAr,plateNumberEn,profileTypeId,qrId,referenceNumber,regionId,saudiMobile,showInMeetLikeYou",
    ];

    [Fact]
    public void EveryPublishedAppJsonKeyIsStillEmitted()
    {
        var actual = new SortedDictionary<string, string[]>(StringComparer.Ordinal);
        var visited = new HashSet<Type>();

        foreach (var contract in AppResponseContracts)
        {
            Collect(contract, actual, visited);
        }

        var published = ParsePublishedWire();
        var breaks = new List<string>();

        foreach (var (contract, keys) in published)
        {
            if (!actual.TryGetValue(contract, out var emitted))
            {
                breaks.Add(
                    $"CONTRACT GONE — {contract} is no longer reachable from any /app/* " +
                    "endpoint, so none of its keys reach the app any more.");
                continue;
            }

            foreach (var key in keys.Where(key => !emitted.Contains(key, StringComparer.Ordinal)))
            {
                breaks.Add($"KEY GONE — {contract} no longer emits \"{key}\".");
            }
        }

        foreach (var (contract, keys) in actual.Where(entry => !published.ContainsKey(entry.Key)))
        {
            breaks.Add(
                $"CONTRACT NOT PINNED — {contract} reaches the app but is not on the " +
                $"published list. Add this line to {nameof(PublishedWire)}:{Environment.NewLine}" +
                $"        \"{contract}: {string.Join(",", keys)}\",");
        }

        Assert.True(breaks.Count == 0, BuildFailureMessage(breaks));
    }

    private static string BuildFailureMessage(IEnumerable<string> breaks) =>
        string.Join(
            Environment.NewLine,
            [
                "The app-facing wire contract is append-only and this change breaks it.",
                string.Empty,
                "The deployed Flutter app decodes these JSON keys BY NAME and is not",
                "rebuilt when the API is. Removing or renaming a key does not fail at",
                "compile time and does not fail at request time — the app simply reads",
                "null for that field on every install already in the field.",
                string.Empty,
                "ADDING a key to a contract that is already pinned is always safe and needs",
                "no edit to this test — that is what append-only means. A brand-new contract",
                "costs one pasted line, so that no future contract escapes the pin.",
                string.Empty,
                "If a key really must go, delete it from PublishedWire in the same commit",
                "and say in the message which app release stopped reading it.",
                string.Empty,
                .. breaks,
            ]);

    /// <summary>
    /// Walks a contract and everything it can carry, recording the JSON key names
    /// System.Text.Json would emit for each object it meets.
    /// </summary>
    private static void Collect(
        Type type, IDictionary<string, string[]> found, ISet<Type> visited)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;

        if (!visited.Add(target))
        {
            return;
        }

        JsonTypeInfo info;

        try
        {
            info = WireOptions.GetTypeInfo(target);
        }
        catch (NotSupportedException)
        {
            // A type System.Text.Json cannot map carries no keys of its own.
            return;
        }

        if (info.Kind == JsonTypeInfoKind.Object)
        {
            found[Describe(target)] = info.Properties
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            foreach (var property in info.Properties)
            {
                Collect(property.PropertyType, found, visited);
            }

            return;
        }

        // A list, an array or a dictionary: the keys live on what it holds.
        if (info.ElementType is not null)
        {
            Collect(info.ElementType, found, visited);
        }

        if (info.KeyType is not null)
        {
            Collect(info.KeyType, found, visited);
        }
    }

    /// <summary>A readable, stable name for a type, generics included.</summary>
    private static string Describe(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var definition = type.GetGenericTypeDefinition().FullName ?? type.Name;
        var arguments = type.GetGenericArguments().Select(Describe);

        return $"{definition[..definition.IndexOf('`', StringComparison.Ordinal)]}<{string.Join(",", arguments)}>";
    }

    private static SortedDictionary<string, string[]> ParsePublishedWire()
    {
        var published = new SortedDictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var line in PublishedWire)
        {
            var separator = line.IndexOf(':', StringComparison.Ordinal);

            Assert.True(
                separator > 0,
                $"{nameof(PublishedWire)} line is not \"Full.Type.Name: key,key\": {line}");

            var contract = line[..separator].Trim();
            var keys = line[(separator + 1)..]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            published.Add(contract, keys);
        }

        return published;
    }

    private static JsonSerializerOptions BuildWireOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new SaudiDateTimeOffsetJsonConverter());

        // Resolve type metadata exactly the way serialization would, so the names
        // read here are the names written on the wire.
        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }
}
