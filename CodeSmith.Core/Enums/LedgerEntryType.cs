// == Ledger Entry Type Enum == //
namespace CodeSmith.Core.Enums;

public enum LedgerEntryType
{
    Spend = 0,   // An LLM call debited from PaidCreditsBalance (default; keeps existing rows/writes correct)
    TopUp = 1    // A Stripe credit purchase added to PaidCreditsBalance
}
