using System.Threading.Tasks;

namespace M.EventBrokerSlim;

/// <summary>
/// Inject as a delegate event handler parameter and use to call next delegate in event handling pipeline.
/// </summary>
public interface INextHandler
{
    /// <summary>
    /// Executes the next delegate in the event handling pipeline.
    /// </summary>
    /// <returns>The task object representing the asynchronous operation.</returns>
    Task Execute();
}
