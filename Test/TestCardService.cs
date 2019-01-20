using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CreditSuisse.Test
{
    using DomainModel;

    [TestFixture]
    public class TestCardService
    {
        // Issue Card with initial deposit/balance.
        [TestCaseSource(typeof(TestCardService), "IssueCardTestCases")]
        public void IssueCard(
            decimal initialDeposit
            )
        {
            const int pin = 1111;
            var service = NewCardService();
            var card = service.IssueCard(
                pin,
                initialDeposit);

            Assert.That(card, Is.Not.Null);

            var balance = 0m;
            Assert.That(
                service.Balance(
                    card,
                    pin,
                    out balance),
                Is.EqualTo(BalanceResult.Success));
            Assert.That(balance, Is.EqualTo(initialDeposit));
        }

        // 1. Can withdraw money if a valid PIN is supplied."
        [TestCaseSource(typeof(TestCardService), "ValidatePinTestCases")]
        public void ValidatePin(
            int            cardPin,
            int            submittedPin,
            WithdrawResult expectedWithdrawResult
            )
        {
            const decimal initialDeposit = 1m;
            var service = NewCardService();
            var card = service.IssueCard(
                cardPin,
                initialDeposit);

            Assert.That(
                service.Withdraw(
                    card,
                    submittedPin,
                    initialDeposit),
                Is.EqualTo(expectedWithdrawResult));
        }

        // 1. The balance on the card needs to adjust accordingly.
        [TestCaseSource(typeof(TestCardService), "WithdrawTestCases")]
        public void Withdraw(
            decimal        beforeBalance,
            decimal        requestedAmount,
            decimal        expectedBalance,
            WithdrawResult expectedWithdrawResult
            )
        {
            const int pin = 1111;
            var service = NewCardService();
            var card = service.IssueCard(
                pin,
                beforeBalance);

            Assert.That(
                service.Withdraw(
                    card,
                    pin,
                    requestedAmount),
                Is.EqualTo(expectedWithdrawResult));

            var balance = 0m;
            Assert.That(
                service.Balance(
                    card,
                    pin,
                    out balance),
                Is.EqualTo(BalanceResult.Success));
            Assert.That(balance, Is.EqualTo(expectedBalance));
        }

        // 2. Can be topped up any time by an arbitrary amount.
        [TestCaseSource(typeof(TestCardService), "DepositTestCases")]
        public void Deposit(
            decimal beforeBalance,
            decimal depositedAmount,
            decimal expectedBalance
            )
        {
            const int pin = 1111;
            var service = NewCardService();
            var card = service.IssueCard(
                pin,
                beforeBalance);

            Assert.That(
                service.Deposit(
                    card,
                    pin,
                    depositedAmount),
                Is.EqualTo(DepositResult.Success));

            var balance = 0m;
            Assert.That(
                service.Balance(
                    card,
                    pin,
                    out balance),
                Is.EqualTo(BalanceResult.Success));
            Assert.That(balance, Is.EqualTo(expectedBalance));
        }

        // 3. The cash card, being virtual, can be used in many places at the same time.
        [Test]
        public void ConcurrentTransactions()
        {
            // Not sure what "many places" means but I'll test that the service can handle concurrent transactions
            // by interleaving them.

            // Use 2 threads to perform 2 actions.
            // Suspend 2nd thread until 1st thread has entered critical region.
            // Then sleep 1st thread (long?) enough for 2nd thread to catch up to critical region.
            // Test that 2nd thread does not enter critical region before 1st thread has exited critical region.

            // Test success indicates:
            // Lock statement prevented 2nd thread entering critical region while 1st thread was in critical region.
            // OR, 2nd thread reached critical region after 1st thread exited critical region.
            // OR, some unforeseen scenario!
            const int pin = 1111;
            var service = NewCardService();
            var card = service.IssueCard(
                pin,
                3m);

            var balanceTrace = new List<decimal>();
            var entered = 0;
            var autoResetEvent = new AutoResetEvent(false);
            var bank = (Bank)service;

            using(var threadId = new ThreadLocal<int>())
            {
                bank.CriticalRegionAfterEnter = account =>
                {
                    Assert.That(Monitor.IsEntered(account));
                    Assert.That(Interlocked.Increment(ref entered), Is.EqualTo(1));

                    if(threadId.Value == 0)
                    {
                        autoResetEvent.Set();
                        Thread.Sleep(250);
                    }
                };

                bank.CriticalRegionBeforeExit = account =>
                {
                    balanceTrace.Add(account.Balance);
                    Assert.That(Interlocked.Decrement(ref entered), Is.EqualTo(0));
                };

                Parallel.Invoke(
                    () =>
                    {
                        threadId.Value = 0;

                        Assert.That(
                            service.Withdraw(
                                card,
                                pin,
                                1m),
                            Is.EqualTo(WithdrawResult.Success));
                    },
                    () =>
                    {
                        threadId.Value = 1;
                        autoResetEvent.WaitOne();
                        autoResetEvent.Set();

                        Assert.That(
                            service.Withdraw(
                                card,
                                pin,
                                2m),
                            Is.EqualTo(WithdrawResult.Success));
                    });
            }

            Assert.That(balanceTrace.SequenceEqual(new[] { 2m, 0m }));
        }

        public static IEnumerable<decimal> IssueCardTestCases
        {
            get
            {
                var random = new Random(0);
                return Enumerable.Range(0, 5)
                    .Select(index => 0m + random.Next(1, 10000))
                    .Select(amount => amount / 100)
                    .ToList();
            }
        }

        public static IEnumerable<object[]> ValidatePinTestCases
        {
            get
            {
                var random = new Random(0);
                var pins = Enumerable.Range(0, 3).Select(index => random.Next(1000, 9999)).ToList();
                return (
                    from cardPin in pins
                    from submittedPin in pins
                    select new object[]
                    {
                        cardPin,
                        submittedPin,
                        submittedPin == cardPin ? WithdrawResult.Success : WithdrawResult.InvalidPin
                    }).ToList();
            }
        }

        public static IEnumerable<object[]> WithdrawTestCases
        {
            get
            {
                var random = new Random(0);
                var amounts = Enumerable.Range(0, 5)
                    .Select(index => 0m + random.Next(1, 10000))
                    .Select(amount => amount / 100)
                    .ToList();
                return (
                    from beforeBalance in amounts
                    from requestedAmount in amounts
                    select new object[]
                    {
                        beforeBalance,
                        requestedAmount,
                        beforeBalance >= requestedAmount ? beforeBalance - requestedAmount : beforeBalance,
                        beforeBalance >= requestedAmount ? WithdrawResult.Success : WithdrawResult.InsufficentFunds
                    }).ToList();
            }
        }

        public static IEnumerable<object[]> DepositTestCases
        {
            get
            {
                var random = new Random(0);
                var amounts = Enumerable.Range(0, 5)
                    .Select(index => 0m + random.Next(1, 10000))
                    .Select(amount => amount / 100)
                    .ToList();
                return (
                    from beforeBalance in amounts
                    from depositedAmount in amounts
                    select new object[]
                    {
                        beforeBalance,
                        depositedAmount,
                        beforeBalance + depositedAmount
                    }).ToList();
            }
        }

        private ICardService NewCardService()
        {
            return new Bank();
        }
    }
}
