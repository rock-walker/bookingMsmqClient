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
using System.Data.SqlClient;

namespace BookingMsmqClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Seat[,] _room = new Seat[30,40];
        private readonly string _queueName = ".\\Private$\\SeatsRoom";
        private List<LabelIdMapping> _seats = new List<LabelIdMapping>();
        private List<Seat> _selectedSeats = new List<Seat>();

        private string connectionString = "Data Source=.\\sql2012; Initial Catalog=Cinema; Integrated Security=SSPI";

        public MainWindow()
        {
            InitializeComponent();

            //UI_InitSeats();

            BL_InitSeats();
        }

        private void BL_InitSeats()
        {
            var queue = new MessageQueue(_queueName){
                Formatter = new XmlMessageFormatter(new[] { typeof(Seat) })
            };

            if (!MessageQueue.Exists(_queueName))
            {
                using (queue = MessageQueue.Create(_queueName))
                {
                    queue.Label = "CinemaSeats";
                }
            }
            
            Task.Factory.StartNew(() => PeekMessages(queue))
                .ContinueWith(x =>
                {
                    var enumerable = x.Result;
                    var seats = enumerable as IList<LabelIdMapping> ?? enumerable.ToList();

                    var dbTickets = GetSqlTickets();

                    if (seats.Count == 0 && dbTickets.Count == 0)
                    {
                        UI_InitSeats();
                        return;
                    }

                    //deserialize messsages
                    var messages = seats.Select(labelIdMapping => queue.PeekById(labelIdMapping.Id).Body as Seat).ToList();

                    for (var i = 0; i < 30; i++)
                    {
                        for (var j = 0; j < 40; j++)
                        {
                            var bookedSeat = messages.FirstOrDefault(n => n.Number == j && n.Row == i) ?? new Seat();

                            //TODO: change to dynamic BookingState
                            bookedSeat.ViewModel = DrawSeat(bookedSeat.BookingState);
                            canvas.Children.Add(bookedSeat.ViewModel);
                            Canvas.SetTop(bookedSeat.ViewModel, i*10);
                            Canvas.SetLeft(bookedSeat.ViewModel, j*10);

                            _room[i, j] = bookedSeat;
                        }
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private List<Seat> GetSqlTickets()
        {
            using (var sql = new SqlConnection(connectionString))
            {
                var queryString = "SELECT * from Ticket";
                var command = new SqlCommand(queryString, sql);
                sql.Open();

                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var guid = reader[1];
                    var row = reader[2];
                    var number = reader[3];
                }
                reader.Close();
            }

            return new List<Seat>();
        }

        private void UI_InitSeats()
        {
            for (var i = 0; i < 30; i++)
            {
                for (var j = 0; j < 40; j++)
                {
                    var uiSeat = DrawSeat(BookingState.Free);
                    canvas.Children.Add(uiSeat);
                    Canvas.SetTop(uiSeat, i * 10);
                    Canvas.SetLeft(uiSeat, j * 10);

                    _room[i, j] = new Seat
                    {
                        ViewModel = uiSeat
                    };
                }
            }
        }

        private IEnumerable<LabelIdMapping> PeekMessages(MessageQueue queue)
        {
            using (var msgEnumerator = queue.GetMessageEnumerator2())
            {
                while (msgEnumerator.MoveNext(TimeSpan.FromMilliseconds(100)) && msgEnumerator.Current != null)
                {
                    var labelId = new LabelIdMapping
                    {
                        Id = msgEnumerator.Current.Id,
                        Label = msgEnumerator.Current.Label
                    };

                    Dispatcher.Invoke(new Action<LabelIdMapping>(AddSeatToRoom), labelId);
                }
            }

            return _seats;
        }

        private void AddSeatToRoom(LabelIdMapping labelIdMapping)
        {
            _seats.Add(labelIdMapping);
        }

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
            var seat = (Rectangle) sender;
            var index = canvas.Children.IndexOf(seat);
            var number = index % 40;
            var row = index / 40;

            MessageBox.Show($"Place [r {row}, s {number}] is reserved" + Environment.NewLine + "Submit personal data");

            var checkedRoom = _room[row, number];
            if (checkedRoom.BookingState != BookingState.AwaitingApproval &&
                checkedRoom.BookingState != BookingState.Reserved)
            {
                checkedRoom.Number = number;
                checkedRoom.Row = row;
                _selectedSeats.Add(checkedRoom);

                checkedRoom.BookingState = BookingState.AwaitingApproval;
                checkedRoom.ViewModel.Fill = UpdateState(checkedRoom.BookingState);
            }
        }

        private SolidColorBrush UpdateState(BookingState state)
        {
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

            return actualColor;
        }

        private void btSubmit_Click(object sender, RoutedEventArgs e)
        {
            var queue = new MessageQueue(_queueName);

            var customer = new Customer
            {
                Name = tbName.Text,
                Surname = tbSurname.Text
            };

            if (!_selectedSeats.Any())
            {
                MessageBox.Show("reserve the seat");
                return;
            }

            foreach (var selectedSeat in _selectedSeats)
            {
                var seat = new Seat
                {
                    BookingState = selectedSeat.BookingState,
                    Data = customer,
                    Number = selectedSeat.Number,
                    Row = selectedSeat.Row
                };
                queue.Send(seat);
            }

            _selectedSeats.Clear();
        }
    }
}
