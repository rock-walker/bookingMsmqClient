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
using System.Threading;

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
        private List<Seat> _seatsCancelledByClient = new List<Seat>(); 

        private string connectionString = "Data Source=.\\sql2012; Initial Catalog=Cinema; Integrated Security=SSPI";
        private MessageQueue _queue;

        public MainWindow()
        {
            _queue = new MessageQueue(_queueName);
            _queue.Formatter = new XmlMessageFormatter(new [] {typeof(Seat)});
            InitializeComponent();
            BL_InitSeats();
            Task.Factory.StartNew(RefreshSeats, TaskCreationOptions.AttachedToParent);
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
                            var bookedSeat = messages.FirstOrDefault(n => n.Number == j && n.Row == i)
                                            ?? dbTickets.FirstOrDefault(n => n.Number == j && n.Row == i)
                                            ?? new Seat();

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

        private void RefreshSeats()
        {
            while (true)
            {
                

                var seats = GetSqlTickets();
                var seatsCancelledByAdmin = new List<Seat>();

                foreach (var seat in seats)
                {
                    var ticket = _room[seat.Row, seat.Number];
                    if (ticket == null)
                        continue;

                    if (ticket.BookingState == seat.BookingState)
                        continue;

                    ticket.BookingState = seat.BookingState;
                    if (seat.BookingState == BookingState.Cancelled)
                    {
                        seatsCancelledByAdmin.Add(seat);
                    }

                    Dispatcher.Invoke(() =>
                    {
                        ticket.ViewModel.Fill = UpdateState(seat.BookingState);
                    });

                }

                var queue1 = new MessageQueue(_queueName) { Formatter = new XmlMessageFormatter(new[] { typeof(Seat) }) };

                var messages = PeekMessages(queue1);
                //var pendingApprovalMsgs = messages.Where(labelIdMapping => queue1.Peek(TimeSpan.FromMilliseconds(1)) != null)
                //    .Select(x => (x.Body) as Seat).ToList();

                foreach (var message in messages)
                {
                    Seat msg = null;
                    try
                    {
                        msg = queue1.PeekById(message.Id).Body as Seat;
                    }
                    catch (InvalidOperationException ex)
                    {
                        _seats = new List<LabelIdMapping>();
                        continue;
                    }
                    

                    var ticket = _room[msg.Row, msg.Number];
                    if (ticket == null)
                        continue;

                    if (ticket.BookingState == msg.BookingState)
                        continue;

                    ticket.BookingState = msg.BookingState;
                    if (msg.BookingState == BookingState.Cancelled)
                    {
                        seatsCancelledByAdmin.Add(msg);
                    }

                    Dispatcher.Invoke(() =>
                    {
                        ticket.ViewModel.Fill = UpdateState(msg.BookingState);
                    });
                }

                foreach (var cancelledSeat in _seatsCancelledByClient)
                {
                    if (!seats.Contains(cancelledSeat))
                    {
                        var ticket = _room[cancelledSeat.Row, cancelledSeat.Number];
                        ticket.BookingState = BookingState.Free;

                        Dispatcher.Invoke(() =>
                        {
                            ticket.ViewModel.Fill = UpdateState(ticket.BookingState);
                        });
                    }
                }
                _seatsCancelledByClient.Clear();

                Thread.Sleep(1000);

                foreach (var cancelledSeat in seatsCancelledByAdmin)
                {
                    var queue = new MessageQueue(_queueName);
                    if (seats.Contains(cancelledSeat))
                    {
                        var ticket = _room[cancelledSeat.Row, cancelledSeat.Number];
                        ticket.BookingState = BookingState.Free;
                        queue.Send(cancelledSeat);

                        Dispatcher.Invoke(() =>
                        {
                            ticket.ViewModel.Fill = UpdateState(ticket.BookingState);
                        });
                    }
                }

                seatsCancelledByAdmin.Clear();
            }
        }

        private List<Seat> GetSqlTickets()
        {
            var seats = new List<Seat>();
            using (var sql = new SqlConnection(connectionString))
            {
                var queryString = "SELECT * from Ticket";
                var command = new SqlCommand(queryString, sql);
                sql.Open();

                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var guid = reader[1];
                    var row = (int)reader[2];
                    var number = (int)reader[3];
                    var state = (BookingState)reader[4];

                    seats.Add(new Seat
                    {
                        Row = row,
                        Number = number,
                        BookingState = state
                    });
                }
                reader.Close();
            }

            return seats;
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
            var counter = 0;

            using (var msgEnumerator = queue.GetMessageEnumerator2())
            {
                while (msgEnumerator.MoveNext(TimeSpan.FromMilliseconds(10)) && msgEnumerator.Current != null)
                {
                    counter++;
                    var labelId = new LabelIdMapping
                    {
                        Id = msgEnumerator.Current.Id,
                        Label = msgEnumerator.Current.Label
                    };

                    Dispatcher.Invoke(new Action<LabelIdMapping>(AddSeatToRoom), labelId);
                }
            }

            return (counter > 0) ? _seats : new List<LabelIdMapping>();
        }

        private void AddSeatToRoom(LabelIdMapping labelIdMapping)
        {
            if (_seats.All(x => x.Id != labelIdMapping.Id))
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

            rect.Fill = UpdateState(state);
            rect.MouseDown += DetectSeat;

            return rect;
        }

        private void DetectSeat(object sender, MouseButtonEventArgs args)
        {
            var seat = (Rectangle) sender;
            var index = canvas.Children.IndexOf(seat);
            var number = index % 40;
            var row = index / 40;

            var checkedRoom = _room[row, number];

            if (checkedRoom.BookingState == BookingState.Reserved)
            {
                if (MessageBox.Show("Do you want to cancel reservation?", "Cancellation", MessageBoxButton.OKCancel) ==
                    MessageBoxResult.OK)
                {
                    var queue = new MessageQueue(_queueName);
                    queue.Send(checkedRoom);

                    checkedRoom.BookingState = BookingState.Cancelled;
                    checkedRoom.ViewModel.Fill = UpdateState(checkedRoom.BookingState);

                    _seatsCancelledByClient.Add(checkedRoom);
                }
                return;
            }

            //MessageBox.Show($"Place [r {row}, s {number}] is reserved" + Environment.NewLine + "Submit personal data");

            if (checkedRoom.BookingState == BookingState.Free)
            {
                checkedRoom.Number = number;
                checkedRoom.Row = row;
                _selectedSeats.Add(checkedRoom);

                checkedRoom.BookingState = BookingState.AwaitingApproval;
                checkedRoom.ViewModel.Fill = UpdateState(checkedRoom.BookingState);

                var queue = new MessageQueue(_queueName);
                queue.Send(checkedRoom);
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
                case BookingState.Cancelled:
                    actualColor.Color = Colors.Yellow;
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

            MessageBox.Show("Wait for approval");

            _selectedSeats.Clear();
        }
    }
}
