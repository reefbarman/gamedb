namespace GameDBLibrary
{
#if FREE_VERSION
    internal
#else
    public
#endif
    interface IFormFactory {
        IForm CreateNewForm();
    }
}