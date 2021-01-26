class MyClass : IDisposable
{
    private bool _disposed = false;

	//手动调用
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

	//GC调用
    ~MyClass()
    {
        Dispose(false);
    }

	protected virtual void Dispose(bool manual)
    {
        if (_disposed)
            return;
        if (manual)
        {
            //TODO:释放托管资源
        }
        //TODO:释放非托管资源
        _disposed = true;
    }
}
