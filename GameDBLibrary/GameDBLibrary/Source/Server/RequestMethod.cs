namespace GameDBLibrary
{
#if FREE_VERSION
    internal
#else
    public
#endif
    enum RequestMethod
    {
        POST,
        GET
    }
}
