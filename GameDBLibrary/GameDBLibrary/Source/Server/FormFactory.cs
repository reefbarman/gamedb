namespace GameDBLibrary {

#if FREE_VERSION
    internal
#else
    public
#endif
    class FormFactory : IFormFactory {
        public IForm CreateNewForm() {
            return new PostForm();
        }
    }
}
