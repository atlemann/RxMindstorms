namespace System.Reactive.Disposables
{
    public static class DisposableExtensions
    {
        public static T DisposeWith<T>(this T @this, CompositeDisposable compositeDisposable)
            where T : IDisposable
        {
            compositeDisposable.Add(@this);
            return @this;
        }
    }
}