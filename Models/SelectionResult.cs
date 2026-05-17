namespace TrayTranslator.Models
{
    public class SelectionResult
    {
        public bool Success { get; set; }
        public string Text { get; set; }
        public bool Truncated { get; set; }
        public string Error { get; set; }

        public static SelectionResult Fail(string error)
        {
            return new SelectionResult { Success = false, Text = "", Error = error };
        }
    }
}
