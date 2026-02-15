namespace Evo.UI
{
    public interface IStylerHandler
    {
        // Points to the local variable
        StylerPreset Preset { get; set; }

        // The method to run when the preset changes
        void UpdateStyler();
    }
}