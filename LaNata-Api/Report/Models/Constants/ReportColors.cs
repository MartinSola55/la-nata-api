using QuestPDF.Infrastructure;

namespace LaNata_Api.Report.Models.Constants
{
    // Los reportes existen para imprimirse, siempre en blanco y negro, y no se guardan como PDF.
    // Por eso la paleta no tiene ningún color de fondo: un relleno se imprime como una mancha gris
    // que solo gasta tinta. La jerarquía se arma con tamaño, negrita y líneas, nunca con rellenos.
    public static class ReportColors
    {
        public const string Primary = "#324B60";
        public const string TextPrimary = "#1B1B1D";
        public const string TextSecondary = "#4A4A52";
        public const string TextMuted = "#70707A";
        public const string Border = "#B9BEC7";

        public static Color ToColor(string hex)
        {
            return Color.FromHex(hex);
        }
    }
}
