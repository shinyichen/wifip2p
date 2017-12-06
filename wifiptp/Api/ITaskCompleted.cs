using System;
namespace wifiptp
{
    public interface ITaskProgress
    {
        void OnFilesReceived();
        void OnTaskCompleted();
    }
}
