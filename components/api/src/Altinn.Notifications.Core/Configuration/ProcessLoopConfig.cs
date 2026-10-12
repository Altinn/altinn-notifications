namespace Altinn.Notifications.Core.Configuration
{
    /// <summary>
    /// Configuration class for process loop settings
    /// </summary>
    public class ProcessLoopConfig
    {
        /// <summary>
        /// The number of tasks to run concurrently in the background service
        /// </summary>
        public int TaskCount { get; set; } = 30;

        /// <summary>
        /// The delay in seconds between each iteration of the background service task when idle for the primary task.
        /// The primary task is the first task that is started and is responsible for triggering additional tasks if needed.
        /// </summary>
        public int PrimaryTaskIdleDelaySeconds { get; set; } = 30;

        /// <summary>
        /// The delay in seconds between each iteration of the background service task for all other tasks
        /// than the primary task.
        /// </summary>
        public int AdditionalTasksIdleDelaySeconds { get; set; } = 5;

        /// <summary>
        /// The number of consecutive tasks before ramping up the processing of tasks. This is used to prevent overloading the system with too many tasks at once.
        /// </summary>
        public int RampUpLimit { get; set; } = 10;
    }
}
