using GameDBLibrary;

namespace GameDBLibraryUnity {
    public class FormFactory : IFormFactory {
        public IForm CreateNewForm() {
            return new UnityForm();
        }
    }
}
