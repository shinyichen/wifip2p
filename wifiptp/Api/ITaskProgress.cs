using System;
namespace wifiptp
{
    public interface ITaskProgress
    {
        void OnConnected(bool server);
        void OnFilesReceived();
        void OnDisconnected(bool server);
    }
}
