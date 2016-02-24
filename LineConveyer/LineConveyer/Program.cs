using System;
using System.Collections.Generic;
using System.Messaging;
using BookingMsmqClient.Models;

namespace BookingMsmqClient
{
    class Program
    {
        private static readonly string _queueName = ".\\Private$\\SeatsRoom";

        static void Main(string[] args)
        {
            ReadQueue();
        }

        private static void ReadQueue()
        {
            using (var queue = new MessageQueue(_queueName))
            {
                queue.Formatter = new XmlMessageFormatter(new[] { typeof(Seat) });
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
