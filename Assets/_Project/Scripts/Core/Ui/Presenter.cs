using System;

namespace Template.Core.Ui
{
    /// <summary>
    /// MVP presenter: holds screen logic against a view interface, so it can be unit-tested
    /// without Unity. The Unity view only forwards input events and displays values.
    /// </summary>
    public abstract class Presenter<TView> : IDisposable where TView : class
    {
        protected TView View { get; private set; }

        public bool IsAttached => View != null;

        public void Attach(TView view)
        {
            if (View != null)
            {
                throw new InvalidOperationException("Presenter is already attached to a view.");
            }

            View = view ?? throw new ArgumentNullException(nameof(view));
            OnAttach();
        }

        public void Dispose()
        {
            if (View == null)
            {
                return;
            }

            OnDetach();
            View = null;
        }

        protected virtual void OnAttach()
        {
        }

        protected virtual void OnDetach()
        {
        }
    }
}
