using System;

namespace BookingMsmqClient.Models
{
    [Serializable]
    class Customer
    {
        public Guid OrderId { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
    }
}
