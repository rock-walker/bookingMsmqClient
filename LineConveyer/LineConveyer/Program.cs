using System;
using System.Collections.Generic;
using System.Messaging;
using BookingMsmqClient.Models;
using System.Data.SqlClient;

namespace BookingMsmqClient
{
    class Program
    {
        private static readonly string _queueName = ".\\Private$\\SeatsRoom";
        private static string connectionString = "Data Source=.\\sql2012; Initial Catalog=Cinema; Integrated Security=SSPI";

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

                    UpdateDb(msg, item.Id);
                }
            }
        }


        private static void UpdateDb(Seat seat, string id)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                var state = BookingState.Reserved;

                var query = string.Format("INSERT INTO Ticket ([UniqueNumber], [Row], [Number], [BookingState]) VALUES (\'{0}\', {1}, {2}, {3})", id, seat.Row, seat.Number, (int)state);
                var command = new SqlCommand(query, connection);
                connection.Open();

                command.ExecuteScalar();
            }
        }
    }
}
