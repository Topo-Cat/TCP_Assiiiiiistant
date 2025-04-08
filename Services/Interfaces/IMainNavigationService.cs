using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlcCommunicator.Services.Interfaces
{
    public interface IMainNavigationService
    {
        void NavigateTo(string pageName, bool clearHistory = true);

        void GoBack();

        void GoForward();

        bool CanGoBack();

        bool CanGoForward();

        void ClearHistory();
    }
}
