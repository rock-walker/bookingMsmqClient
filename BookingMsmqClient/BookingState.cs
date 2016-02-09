using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BookingMsmqClient
{
    [Flags]
    public enum BookingState
    {
        Free,
        AwaitingApproval,
        Reserved,
    }
}
