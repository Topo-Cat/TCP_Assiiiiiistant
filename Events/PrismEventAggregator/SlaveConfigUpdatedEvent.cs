using ModbusCommunicator.Services.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusCommunicator.Events.PrismEventAggregator
{
    public class SlaveConfigUpdatedEvent : PubSubEvent<ModbusTcpSlaveOptions> { }
}
