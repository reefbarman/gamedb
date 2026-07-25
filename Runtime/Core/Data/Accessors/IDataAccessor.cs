namespace GameDBLibrary
{
    public interface IDataAccessor {}

    public abstract class DataAccessor<T> : IDataAccessor
    {
        public abstract T GetValue();
    }
}
