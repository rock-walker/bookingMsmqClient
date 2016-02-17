using System.ComponentModel;
using System.Windows.Shapes;
using System.Xml.Serialization;

namespace BookingMsmqClient.Models
{
    public class Seat
    {
        public int Number { get; set; }
        public int Row { get; set; }
        public BookingState BookingState { get; set; }
        public Customer Data { get; set; }
        [XmlIgnore]
        public Rectangle ViewModel { get; set; }
    }
}
