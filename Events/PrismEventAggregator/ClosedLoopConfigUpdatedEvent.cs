using PlcCommunicator.Services.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlcCommunicator.Events.PrismEventAggregator
{
    public class ClosedLoopConfigUpdatedEvent : PubSubEvent<ModbusTcpClosedLoopOptions> { }
}
