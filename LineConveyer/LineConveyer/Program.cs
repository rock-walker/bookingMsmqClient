using System;
using System.Messaging;
using BookingMsmqClient.Models;
using System.Data.SqlClient;
using System.Threading;

namespace BookingMsmqClient
{
    class Program
    {
        private static readonly string _queueName = ".\\Private$\\SeatsRoom";
        private static string connectionString = "Data Source=.\\sql2012; Initial Catalog=Cinema; Integrated Security=SSPI";

        static void Main(string[] args)
        {
            while (true)
            {
                Console.WriteLine("Reading message...");
                ReadQueue();
                Console.WriteLine("Queue is busy");
                Thread.Sleep(5000);
            }
        }

        private static void ReadQueue()
        {
            using (var queue = new MessageQueue(_queueName))
            {
                queue.Formatter = new XmlMessageFormatter(new[] { typeof(Seat) });

                try
                {
                    var msg = queue.Receive(TimeSpan.FromSeconds(5));
                    var seat = msg.Body as Seat;

                    Console.WriteLine($"Received message: Number->{seat.Number}, Row->{seat.Row}");

                    UpdateDb(seat, msg.Id);
                }
                catch (MessageQueueException ex)
                {
                    Console.WriteLine("No messages in queue");
                }
            }
        }


        private static void UpdateDb(Seat seat, string id)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                var state = BookingState.Reserved;

                var query =
                    $"INSERT INTO Ticket ([UniqueNumber], [Row], [Number], [BookingState]) VALUES (\'{id}\', {seat.Row}, {seat.Number}, {(int) state})";
                var command = new SqlCommand(query, connection);
                connection.Open();

                command.ExecuteScalar();
                connection.Close();
            }
        }
    }
}
