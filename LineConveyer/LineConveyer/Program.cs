using System;
using System.Collections.Generic;
using System.Messaging;
using BookingMsmqClient.Models;

namespace BookingMsmqClient
{
    class Program
    {
        private readonly string _queueName = ".\\Private$\\SeatsRoom";

        static void Main(string[] args)
        {

        }

        private void ReadQueue()
        {
            using (var queue = new MessageQueue(_queueName))
            {
                var enumerator = queue.GetMessageEnumerator2();

                while (enumerator.MoveNext(TimeSpan.FromMilliseconds(10000)) && enumerator.Current != null)
                {
                    var item = enumerator.Current;
                    var msg = item.Body as Seat;

                }

            }
        }
    }
}
