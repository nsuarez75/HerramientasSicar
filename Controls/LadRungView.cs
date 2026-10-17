using HerramientasSICAR.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace HerramientasSICAR.Controls
{
    /// <summary>
    /// Draws a boolean expression as a LAD rung (AND = series, OR = parallel branches)
    /// ending in a coil, with the power flow for the given signal state.
    /// Clicking a contact executes ToggleCommand with the signal name.
    /// </summary>
    public class LadRungView : FrameworkElement
    {
        private const double AnchoCelda = 128;
        private const double AltoCelda = 58;
        private const double Margen = 10;
        private const double AnchoBobina = 170;
        private const double AnchoContacto = 18;

        private static readonly Brush _energizado = Freeze(new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)));
        private static readonly Brush _apagado = Freeze(new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)));
        private static readonly Brush _cerrado = Freeze(new SolidColorBrush(Color.FromRgb(0xC8, 0xE6, 0xC9)));
        private static readonly Brush _texto = Freeze(new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21)));
        private static readonly Brush _hover = Freeze(new SolidColorBrush(Color.FromArgb(0x22, 0x21, 0x96, 0xF3)));
        private static readonly Brush _clave = Freeze(new SolidColorBrush(Color.FromRgb(0xE6, 0x51, 0x00)));
        private static readonly Brush _claveFondo = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0xB2)));
        private static readonly Typeface _fuente = new Typeface("Segoe UI");

        private readonly List<(Rect Zona, string Variable)> _contactos = new List<(Rect, string)>();
        private string _variableHover;

        public static readonly DependencyProperty ExpresionProperty = DependencyProperty.Register(
            nameof(Expresion), typeof(ExpresionLogica), typeof(LadRungView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BobinaProperty = DependencyProperty.Register(
            nameof(Bobina), typeof(string), typeof(LadRungView),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty EstadoProperty = DependencyProperty.Register(
            nameof(Estado), typeof(IDictionary<string, bool>), typeof(LadRungView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SenalesClaveProperty = DependencyProperty.Register(
            nameof(SenalesClave), typeof(ISet<string>), typeof(LadRungView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ToggleCommandProperty = DependencyProperty.Register(
            nameof(ToggleCommand), typeof(ICommand), typeof(LadRungView));

        public ExpresionLogica Expresion
        {
            get => (ExpresionLogica)GetValue(ExpresionProperty);
            set => SetValue(ExpresionProperty, value);
        }

        public string Bobina
        {
            get => (string)GetValue(BobinaProperty);
            set => SetValue(BobinaProperty, value);
        }

        public IDictionary<string, bool> Estado
        {
            get => (IDictionary<string, bool>)GetValue(EstadoProperty);
            set => SetValue(EstadoProperty, value);
        }

        /// <summary>Signals highlighted as decisive; when set, the labels of the other contacts are dimmed.</summary>
        public ISet<string> SenalesClave
        {
            get => (ISet<string>)GetValue(SenalesClaveProperty);
            set => SetValue(SenalesClaveProperty, value);
        }

        public ICommand ToggleCommand
        {
            get => (ICommand)GetValue(ToggleCommandProperty);
            set => SetValue(ToggleCommandProperty, value);
        }

        #region Layout

        private static (int Ancho, int Alto) Medir(ExpresionLogica e)
        {
            switch (e)
            {
                case ExpresionLogica.Conjuncion y:
                    var hijosY = y.Operandos.Select(Medir).ToList();
                    return (hijosY.Sum(h => h.Ancho), hijosY.Max(h => h.Alto));
                case ExpresionLogica.Disyuncion o:
                    var hijosO = o.Operandos.Select(Medir).ToList();
                    return (hijosO.Max(h => h.Ancho), hijosO.Sum(h => h.Alto));
                case ExpresionLogica.Negacion n when !(n.Operando is ExpresionLogica.Variable):
                    var interior = Medir(n.Operando);
                    return (interior.Ancho + 1, interior.Alto);
                default:
                    return (1, 1);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var e = Expresion ?? ExpresionLogica.True;
            var (ancho, alto) = Medir(e);
            return new Size(Margen * 2 + ancho * AnchoCelda + AnchoBobina, alto * AltoCelda + 6);
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            _contactos.Clear();
            var e = Expresion ?? ExpresionLogica.True;
            var (ancho, alto) = Medir(e);

            // Left powerrail
            dc.DrawLine(new Pen(_energizado, 3), new Point(Margen, 4), new Point(Margen, alto * AltoCelda));

            bool salida = Dibujar(dc, e, 0, 0, ancho, true);

            // Coil
            double y = YLinea(0);
            double x = Margen + ancho * AnchoCelda;
            var pen = Lapiz(salida);
            dc.DrawLine(pen, new Point(x, y), new Point(x + 24, y));
            var arco = new StreamGeometry();
            using (var ctx = arco.Open())
            {
                ctx.BeginFigure(new Point(x + 30, y - 11), false, false);
                ctx.ArcTo(new Point(x + 30, y + 11), new Size(14, 14), 0, false, SweepDirection.Counterclockwise, true, false);
                ctx.BeginFigure(new Point(x + 42, y - 11), false, false);
                ctx.ArcTo(new Point(x + 42, y + 11), new Size(14, 14), 0, false, SweepDirection.Clockwise, true, false);
            }
            if (salida)
                dc.DrawEllipse(_cerrado, null, new Point(x + 36, y), 9, 9);
            dc.DrawGeometry(null, new Pen(salida ? _energizado : _texto, 2), arco);
            DibujarTexto(dc, Bobina ?? "", x, y - 32, AnchoBobina - 8, TextAlignment.Left, salida ? FontWeights.Bold : FontWeights.Normal);
        }

        private bool Dibujar(DrawingContext dc, ExpresionLogica e, int col, int fila, int anchoAsignado, bool entrada)
        {
            double x0 = Margen + col * AnchoCelda;
            double xFin = Margen + (col + anchoAsignado) * AnchoCelda;
            double y = YLinea(fila);

            switch (e)
            {
                case ExpresionLogica.Conjuncion conj:
                    {
                        bool energia = entrada;
                        int c = col;
                        for (int i = 0; i < conj.Operandos.Count; i++)
                        {
                            var hijo = conj.Operandos[i];
                            int ancho = i == conj.Operandos.Count - 1 ? col + anchoAsignado - c : Medir(hijo).Ancho;
                            energia = Dibujar(dc, hijo, c, fila, ancho, energia);
                            c += Medir(hijo).Ancho;
                        }
                        return energia;
                    }

                case ExpresionLogica.Disyuncion disy:
                    {
                        bool salida = false;
                        int f = fila;
                        double yUltima = y;
                        foreach (var rama in disy.Operandos)
                        {
                            salida |= Dibujar(dc, rama, col, f, anchoAsignado, entrada);
                            yUltima = YLinea(f);
                            f += Medir(rama).Alto;
                        }
                        dc.DrawLine(Lapiz(entrada), new Point(x0, y), new Point(x0, yUltima));
                        dc.DrawLine(Lapiz(salida), new Point(xFin, y), new Point(xFin, yUltima));
                        return salida;
                    }

                case ExpresionLogica.Negacion n when !(n.Operando is ExpresionLogica.Variable):
                    {
                        // NOT instruction after the inner network: it inverts the RLO
                        bool interior = Dibujar(dc, n.Operando, col, fila, anchoAsignado - 1, entrada);
                        bool salida = entrada && !n.Operando.Evaluar(EstadoActual);
                        double xc = xFin - AnchoCelda;
                        DibujarCable(dc, xc, xc + AnchoCelda / 2 - AnchoContacto / 2, y, interior);
                        DibujarCaja(dc, "NOT", xc + AnchoCelda / 2, y, salida);
                        DibujarCable(dc, xc + AnchoCelda / 2 + AnchoContacto / 2, xFin, y, salida);
                        return salida;
                    }

                case ExpresionLogica.Constante k:
                    {
                        bool salida = entrada && k.Valor;
                        if (k.Valor)
                            DibujarCable(dc, x0, xFin, y, salida);
                        return salida;
                    }

                default:
                    {
                        bool negado = e is ExpresionLogica.Negacion;
                        string variable = negado ? ((ExpresionLogica.Negacion)e).Operando.ToString() : e.ToString();
                        bool valor = EstadoActual.TryGetValue(variable, out bool v) && v;
                        bool cerrado = negado ? !valor : valor;
                        bool salida = entrada && cerrado;
                        DibujarContacto(dc, x0, y, variable, negado, valor, cerrado, entrada, salida);
                        DibujarCable(dc, x0 + AnchoCelda, xFin, y, salida);
                        return salida;
                    }
            }
        }

        private void DibujarContacto(DrawingContext dc, double x0, double y, string variable, bool negado, bool valor, bool cerrado, bool entrada, bool salida)
        {
            double xc = x0 + AnchoCelda / 2;
            double xa = xc - AnchoContacto / 2;
            double xb = xc + AnchoContacto / 2;

            var zona = new Rect(x0 + 2, y - AltoCelda + 14, AnchoCelda - 4, AltoCelda - 2);
            _contactos.Add((zona, variable));

            bool hayClaves = SenalesClave != null && SenalesClave.Count > 0;
            bool esClave = hayClaves && SenalesClave.Contains(variable);
            if (esClave)
                dc.DrawRoundedRectangle(_claveFondo, new Pen(_clave, 1.5), zona, 4, 4);
            if (variable == _variableHover)
                dc.DrawRoundedRectangle(_hover, null, zona, 4, 4);

            DibujarCable(dc, x0, xa, y, entrada);
            DibujarCable(dc, xb, x0 + AnchoCelda, y, salida);

            if (cerrado)
                dc.DrawRectangle(_cerrado, null, new Rect(xa, y - 11, AnchoContacto, 22));
            var barra = new Pen(cerrado ? _energizado : _texto, 2);
            dc.DrawLine(barra, new Point(xa, y - 11), new Point(xa, y + 11));
            dc.DrawLine(barra, new Point(xb, y - 11), new Point(xb, y + 11));
            if (negado)
                dc.DrawLine(barra, new Point(xa + 3, y + 9), new Point(xb - 3, y - 9));

            DibujarTexto(dc, NombreVisible(variable), x0 + 4, y - 40, AnchoCelda - 8, TextAlignment.Center,
                         valor || esClave ? FontWeights.Bold : FontWeights.Normal,
                         esClave ? _clave : hayClaves ? _apagado : null);
            DibujarTexto(dc, valor ? "1" : "0", xb + 3, y + 2, 12, TextAlignment.Left, FontWeights.Normal, valor ? _energizado : _apagado);
        }

        private void DibujarCaja(DrawingContext dc, string texto, double xc, double y, bool salida)
        {
            var rect = new Rect(xc - AnchoContacto, y - 11, AnchoContacto * 2, 22);
            dc.DrawRectangle(salida ? _cerrado : Brushes.White, new Pen(salida ? _energizado : _texto, 1.5), rect);
            DibujarTexto(dc, texto, rect.X, y - 8, rect.Width, TextAlignment.Center, FontWeights.SemiBold);
        }

        private static void DibujarCable(DrawingContext dc, double xDesde, double xHasta, double y, bool energizado)
        {
            if (xHasta > xDesde)
                dc.DrawLine(Lapiz(energizado), new Point(xDesde, y), new Point(xHasta, y));
        }

        private void DibujarTexto(DrawingContext dc, string texto, double x, double y, double ancho, TextAlignment alineacion,
                                  FontWeight peso, Brush color = null)
        {
            var ft = new FormattedText(texto, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _fuente, 10.5,
                                       color ?? _texto, VisualTreeHelper.GetDpi(this).PixelsPerDip)
            {
                MaxTextWidth = Math.Max(1, ancho),
                MaxLineCount = 2,
                Trimming = TextTrimming.CharacterEllipsis,
                TextAlignment = alineacion
            };
            ft.SetFontWeight(peso);
            dc.DrawText(ft, new Point(x, y));
        }

        private static string NombreVisible(string variable) => variable.Replace("\"", "");

        private static double YLinea(int fila) => fila * AltoCelda + AltoCelda - 16;

        private static Pen Lapiz(bool energizado) => energizado ? new Pen(_energizado, 2.5) : new Pen(_apagado, 1.5);

        private IDictionary<string, bool> EstadoActual => Estado ?? new Dictionary<string, bool>();

        private static Brush Freeze(SolidColorBrush b)
        {
            b.Freeze();
            return b;
        }

        #endregion

        #region Interaction

        private string ContactoEn(Point p) => _contactos.FirstOrDefault(c => c.Zona.Contains(p)).Variable;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            string variable = ContactoEn(e.GetPosition(this));
            if (variable == _variableHover)
                return;

            _variableHover = variable;
            Cursor = variable != null ? Cursors.Hand : null;
            ToolTip = variable == null ? null
                : variable + " = " + (EstadoActual.TryGetValue(variable, out bool v) && v ? "1" : "0")
                  + (SenalesClave != null && SenalesClave.Contains(variable) ? "\nSeñal clave: cambiarla sola cambia la coincidencia" : "")
                  + "\nClic para cambiar";
            InvalidateVisual();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _variableHover = null;
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            string variable = ContactoEn(e.GetPosition(this));
            if (variable != null && ToggleCommand?.CanExecute(variable) == true)
                ToggleCommand.Execute(variable);
        }

        // Without a background the element is not hit-testable between the lines
        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters) =>
            new PointHitTestResult(this, hitTestParameters.HitPoint);

        #endregion
    }
}
