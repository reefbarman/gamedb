namespace GameDBLibrary {
#if FREE_VERSION
    internal
#else
    public
#endif
    static class WebRequestHelper
    {
        public static IWebRequest Request { get; set; } = new HTTPRequest();
        public static IFormFactory FormFactory { get; set; } = new FormFactory();
    }
}
