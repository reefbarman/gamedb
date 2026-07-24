namespace GameDBEditorLibrary {
    internal class ConfigurationComponent : Component
    {
        public ConfigurationComponent(string name) : base(name)
        {
            AddChild(new ConfigEnumsComponent("ConfigEnums"));
        }

        public override void Render(params object[] args)
        {
#if !FREE_VERSION
            RenderChild("ConfigEnums");
#endif
        }
    }
}
