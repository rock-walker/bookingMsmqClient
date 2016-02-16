using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;

namespace BookingMsmqClient.Models
{
    class Seat
    {
        public int Number { get; set; }
        public int Row { get; set; }
        public BookingState BookingState { get; set; }
        public Customer Customer { get; set; }
        public Rectangle ViewModel { get; set; }
    }
}
