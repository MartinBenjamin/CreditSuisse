namespace CreditSuisse.DomainModel
{
    public enum BalanceResult
    {
        Success,
        UnrecognisedCard,
        InvalidPin
    }

    public enum WithdrawResult
    {
        Success,
        UnrecognisedCard,
        InvalidPin,
        InsufficentFunds
    }

    public enum DepositResult
    {
        Success,
        UnrecognisedCard,
        InvalidPin
    }

    public interface ICardService
    {
        Card IssueCard(
            int     pin,
            decimal initialDeposit);

        BalanceResult Balance(
            Card        card,
            int         pin,
            out decimal balance);

        WithdrawResult Withdraw(
            Card    card,
            int     pin,
            decimal amount);

        DepositResult Deposit(
            Card    card,
            int     pin,
            decimal amount);
    }
}
