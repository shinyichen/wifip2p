using System;
namespace wifip2pApi.Android
{
    public interface ITaskProgress
    {
        void OnConnected(bool server);
        void OnFilesReceived();
        void OnDisconnected(bool server);
        void OnStatusUpdate(string message);
    }
}
