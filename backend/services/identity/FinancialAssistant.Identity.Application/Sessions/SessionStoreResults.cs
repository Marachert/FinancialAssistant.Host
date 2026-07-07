namespace FinancialAssistant.Identity.Application.Sessions;

public enum SessionRotationStoreResult
{
    Success = 1,
    Missing = 2,
    InvalidSecret = 3,
    Expired = 4,
    Revoked = 5,
    ReuseDetected = 6
}

public enum SessionRevocationStoreResult
{
    Success = 1,
    Missing = 2,
    InvalidSecret = 3,
    Revoked = 4,
    Expired = 5
}
