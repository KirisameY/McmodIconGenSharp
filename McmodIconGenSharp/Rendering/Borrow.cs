namespace McmodIconGenSharp.Rendering;

internal class BorrowRes<T>(T value, Action @return) : IDisposable
{
    public T Value
    {
        get
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            return value;
        }
    }

    public bool Disposed { get; private set; }

    public void Dispose()
    {
        if (Disposed) return;
        Disposed = true;

        @return.Invoke();
    }
}