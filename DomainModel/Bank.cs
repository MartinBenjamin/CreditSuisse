using System;
using System.Collections.Generic;

namespace CreditSuisse.DomainModel
{
    public class Bank: ICardService
    {
        private IDictionary<Card, int    > _pins     = new Dictionary<Card, int    >();
        private IDictionary<Card, Account> _accounts = new Dictionary<Card, Account>();

        // For testing.
        public Action<Account> CriticalRegionAfterEnter { get; set; }
        public Action<Account> CriticalRegionBeforeExit { get; set; }

        Card ICardService.IssueCard(
            int     pin,
            decimal initialDeposit
            )
        {
            var card = new Card();

            _pins[card] = pin;
            _accounts[card]= new Account
            {
                Balance = initialDeposit
            };

            return card;
        }

        BalanceResult ICardService.Balance(
            Card        card,
            int         pin,
            out decimal balance
            )
        {
            balance = 0m;

            var requiredPin = 0;
            if(!_pins.TryGetValue(
                card,
                out requiredPin))
                return BalanceResult.UnrecognisedCard;

            if(pin != requiredPin)
                return BalanceResult.InvalidPin;

            balance = _accounts[card].Balance;
            return BalanceResult.Success;
        }

        WithdrawResult ICardService.Withdraw(
            Card    card,
            int     pin,
            decimal amount
            )
        {
            if(amount < decimal.Zero)
                throw new ArgumentOutOfRangeException("amount");

            var requiredPin = 0;
            if(!_pins.TryGetValue(
                card,
                out requiredPin))
                return WithdrawResult.UnrecognisedCard;

            if(pin != requiredPin)
                return WithdrawResult.InvalidPin;

            var account = _accounts[card];

            lock(account)
            {
                if(CriticalRegionAfterEnter != null)
                    CriticalRegionAfterEnter(account);

                if(amount > account.Balance)
                    return WithdrawResult.InsufficentFunds;

                account.Balance -= amount;

                if(CriticalRegionBeforeExit != null)
                    CriticalRegionBeforeExit(account);
            }

            return WithdrawResult.Success;
        }

        DepositResult ICardService.Deposit(
            Card    card,
            int     pin,
            decimal amount
            )
        {
            if(amount < decimal.Zero)
                throw new ArgumentOutOfRangeException("amount");

            var requiredPin = 0;
            if(!_pins.TryGetValue(
                card,
                out requiredPin))
                return DepositResult.UnrecognisedCard;

            if(pin != requiredPin)
                return DepositResult.InvalidPin;

            var account = _accounts[card];

            lock(account)
            {
                if(CriticalRegionAfterEnter != null)
                    CriticalRegionAfterEnter(account);

                account.Balance += amount;

                if(CriticalRegionBeforeExit != null)
                    CriticalRegionBeforeExit(account);
            }

            return DepositResult.Success;
        }
    }
}
