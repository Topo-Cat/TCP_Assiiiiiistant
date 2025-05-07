using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusCommunicator.Events
{
    public class ModBusErrorEvent : PubSubEvent<ErrorEventArgs>
    {
    }
}
