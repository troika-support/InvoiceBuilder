namespace InvoiceBuilder.Domain.Documents;

public enum DocumentType
{
    Quote = 0,
    Invoice = 1,
    Receipt = 2,
    CreditNote = 3,
}

public enum DocumentStatus
{
    Draft = 0,
    Issued = 1,
    Sent = 2,
    Viewed = 3,
    PartPaid = 4,
    Paid = 5,
    Accepted = 6,
    Declined = 7,
    Expired = 8,
    Converted = 9,
    Void = 10,
}

public enum InstalmentFrequency
{
    Weekly = 0,
    EveryTwoWeeks = 1,
    Monthly = 2,
    Quarterly = 3,
}

public enum InstalmentDueKind
{
    Date = 0,
    OnAcceptance = 1,
    OnIssue = 2,
}

public enum InstalmentStatus
{
    Upcoming = 0,
    Due = 1,
    Overdue = 2,
    PartPaid = 3,
    Paid = 4,
}

/// <summary>"Let the client pay with" chips on a document (DS §8.3 region 6).</summary>
[Flags]
public enum AllowedPaymentMethods
{
    None = 0,
    EcoCash = 1,
    Card = 2,
    BankTransfer = 4,
    Cash = 8,
    OneMoney = 16,
    InnBucks = 32,
    Other = 64,
    All = EcoCash | Card | BankTransfer | Cash | OneMoney | InnBucks | Other,
}

public enum QuoteDecision
{
    Accepted = 0,
    Declined = 1,
}
