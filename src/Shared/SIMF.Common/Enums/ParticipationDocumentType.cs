namespace SIMF.Common.Enums;

/// <summary>Used by the الطلبات desk: the kind of participation
/// document an attendee can request (طلب وثيقة المشاركة). Stored as an int;
/// additive new values are allowed (append-only). The Arabic/English label is
/// rendered by the app + Control Panel from this discriminator.</summary>
public enum ParticipationDocumentType
{
    /// <summary>شهادة حضور رسمية — an official attendance certificate.</summary>
    AttendanceCertificate = 0,

    /// <summary>خطاب مشاركة — a participation letter.</summary>
    ParticipationLetter = 1,

    /// <summary>خطاب دعوة — an invitation letter.</summary>
    InvitationLetter = 2,
}
