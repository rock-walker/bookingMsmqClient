using System;
using System.Collections.Generic;
using System.Linq;
using System.Messaging;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using BookingMsmqClient.Models;

namespace BookingMsmqClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Seat[,] _room = new Seat[30,40];
        private readonly string _queueName = ".\\Private$\\SeatsRoom";

        public MainWindow()
        {
            InitializeComponent();

            BL_InitSeats();
        }

        private void BL_InitSeats()
        {
            if (MessageQueue.Exists(_queueName))
            {
                using (var queue = MessageQueue.Create(_queueName))
                {
                    queue.Label = "CinemaSeats";
                }
            }
            else
            {
                var queue = new MessageQueue(_queueName)
                {
                    Formatter = new XmlMessageFormatter(new [] {typeof(Seat)})
                };

                Task.Factory.StartNew(() => PeekMessages(queue))
                    .ContinueWith(x =>
                    {
                        var enumerable = x.Result;
                        var seats = enumerable as IList<Seat> ?? enumerable.ToList();

                        for (var i = 0; i < 30; i++)
                        {
                            for (var j = 0; j < 40; j++)
                            {
                                var bookedSeat = seats.FirstOrDefault(n => n.Number == j && n.Row == i);

                                //TODO: change to dynamic BookingState
                                bookedSeat.ViewModel = DrawSeat(BookingState.Free);
                                canvas.Children.Add(bookedSeat.ViewModel);
                                Canvas.SetTop(bookedSeat.ViewModel, i*10);
                                Canvas.SetLeft(bookedSeat.ViewModel, j*10);

                                _room[i, j] = bookedSeat;
                            }
                        }
                    });
            }
        }

        private IEnumerable<Seat> PeekMessages(MessageQueue queue)
        {
            var seats = new List<Seat>();
            using (var msgEnumerator = queue.GetMessageEnumerator2())
            {
                while (msgEnumerator.MoveNext(TimeSpan.FromMinutes(1)) && msgEnumerator.Current != null)
                {
                    var labelId = new LabelIdMapping
                    {
                        Id = msgEnumerator.Current.Id,
                        Label = msgEnumerator.Current.Label
                    };

                    var seat = (Seat) msgEnumerator.Current.Body;
                    seats.Add(seat);

                    Dispatcher.Invoke(x => )
                }
            }
            return seats;
        }

        Action<LabelIdMapping> AddSeatToRoom = (x => 
            
        )

        private Rectangle DrawSeat(BookingState state)
        {
            var rect = new Rectangle
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(Colors.White),
                RadiusX = 1.5,
                RadiusY = 1.5,
                Stroke = new SolidColorBrush(Colors.Black),
                StrokeThickness = 0.1
            };

            var actualColor = new SolidColorBrush(Colors.White);
            switch (state)
            {
                case BookingState.Reserved:
                    actualColor.Color = Colors.DarkRed;
                    break;
                case BookingState.AwaitingApproval:
                    actualColor.Color = Colors.DeepPink;
                    break;
                default:
                    actualColor.Color = Colors.White;
                    break;
            }

            rect.Fill = actualColor;
            rect.MouseDown += DetectSeat;

            return rect;
        }

        private void DetectSeat(object sender, MouseButtonEventArgs args)
        {
            
        }
    }
}
