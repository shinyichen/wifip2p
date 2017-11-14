using System;
namespace wifiptp.Api
{
    public interface FileSendingListener
    {
        void sendSuccessful(string path);
        void sendFailed(string path);
    }
}
