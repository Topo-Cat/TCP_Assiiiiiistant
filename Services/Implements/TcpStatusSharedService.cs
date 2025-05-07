using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusCommunicator.Services.Interfaces
{
    public class TcpStatusSharedService
    {
        private readonly object _ClosedLooplock = new object();

        private bool _isClosedLoopServiceReady;
        public bool IsClosedLoopServiceReady
        {
            get
            {
                lock (_ClosedLooplock)
                {
                    return _isClosedLoopServiceReady;
                }
            }
            set
            {
                lock (_ClosedLooplock)
                {
                    _isClosedLoopServiceReady = value;
                }
            }
        }
    }
}
