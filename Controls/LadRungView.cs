using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using HerramientasSICAR.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace HerramientasSICAR.Controls
{
    /// <summary>
    /// Draws a boolean expression as a LAD rung (AND = series, OR = parallel branches)
    /// ending in a coil, with the power flow for the given signal state.
    /// Clicking a contact executes ToggleCommand with the signal name.
    /// </summary>
    public class LadRungView : Control
    {
        private const double AnchoCelda = 128;
        private const double AltoCelda = 58;
        private const double Margen = 10;
        private const double AnchoBobina = 170;
        private const double AnchoContacto = 18;

        private static readonly IBrush Apagado = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E));

        private readonly List<(Rect Zona, string Variable)> _contactos = new List<(Rect, string)>();
        private string _variableHover;

        // Colores del tema (claro u oscuro), leídos en cada repintado.
        private IBrush _texto, _energizado, _cerrado, _clave, _claveFondo, _hover, _superficie;

        public static readonly StyledProperty<ExpresionLogica> ExpresionProperty =
            AvaloniaProperty.Register<LadRungView, ExpresionLogica>(nameof(Expresion));

        public static readonly StyledProperty<string> BobinaProperty =
            AvaloniaProperty.Register<LadRungView, string>(nameof(Bobina), "");

        public static readonly StyledProperty<IDictionary<string, bool>> EstadoProperty =
            AvaloniaProperty.Register<LadRungView, IDictionary<string, bool>>(nameof(Estado));

        public static readonly StyledProperty<ISet<string>> SenalesClaveProperty =
            AvaloniaProperty.Register<LadRungView, ISet<string>>(nameof(SenalesClave));

        public static readonly StyledProperty<ICommand> ToggleCommandProperty =
            AvaloniaProperty.Register<LadRungView, ICommand>(nameof(ToggleCommand));

        // Color del texto: se hereda del contenedor, así sigue al tema.
        public static readonly StyledProperty<IBrush> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<LadRungView>();

        static LadRungView()
        {
            AffectsMeasure<LadRungView>(ExpresionProperty);
            AffectsRender<LadRungView>(ExpresionProperty, BobinaProperty, EstadoProperty, SenalesClaveProperty, ForegroundProperty);
        }

        public LadRungView()
        {
            ActualThemeVariantChanged += (_, __) => InvalidateVisual();
        }

        public ExpresionLogica Expresion
        {
            get => GetValue(ExpresionProperty);
            set => SetValue(ExpresionProperty, value);
        }

        public string Bobina
        {
            get => GetValue(BobinaProperty);
            set => SetValue(BobinaProperty, value);
        }

        public IDictionary<string, bool> Estado
        {
            get => GetValue(EstadoProperty);
            set => SetValue(EstadoProperty, value);
        }

        /// <summary>Signals highlighted as decisive; when set, the labels of the other contacts are dimmed.</summary>
        public ISet<string> SenalesClave
        {
            get => GetValue(SenalesClaveProperty);
            set => SetValue(SenalesClaveProperty, value);
        }

        public ICommand ToggleCommand
        {
            get => GetValue(ToggleCommandProperty);
            set => SetValue(ToggleCommandProperty, value);
        }

        public IBrush Foreground
        {
            get => GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
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
            var (ancho, alto) = Medir(Expresion ?? ExpresionLogica.True);
            return new Size(Margen * 2 + ancho * AnchoCelda + AnchoBobina, alto * AltoCelda + 6);
        }

        #endregion

        #region Rendering

        public override void Render(DrawingContext dc)
        {
            LeerColores();
            _contactos.Clear();

            // Without a background the control is not hit-testable between the lines
            dc.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

            var e = Expresion ?? ExpresionLogica.True;
            var (ancho, alto) = Medir(e);

            // Left powerrail
            dc.DrawLine(new Pen(_energizado, 3), new Point(Margen, 4), new Point(Margen, alto * AltoCelda));

            bool salida = Dibujar(dc, e, 0, 0, ancho, true);

            // Coil
            double y = YLinea(0);
            double x = Margen + ancho * AnchoCelda;
            dc.DrawLine(Lapiz(salida), new Point(x, y), new Point(x + 24, y));
            var arco = new StreamGeometry();
            using (var ctx = arco.Open())
            {
                ctx.BeginFigure(new Point(x + 30, y - 11), false);
                ctx.ArcTo(new Point(x + 30, y + 11), new Size(14, 14), 0, false, SweepDirection.CounterClockwise);
                ctx.EndFigure(false);
                ctx.BeginFigure(new Point(x + 42, y - 11), false);
                ctx.ArcTo(new Point(x + 42, y + 11), new Size(14, 14), 0, false, SweepDirection.Clockwise);
                ctx.EndFigure(false);
            }
            if (salida)
                dc.DrawEllipse(_cerrado, null, new Point(x + 36, y), 9, 9);
            dc.DrawGeometry(null, new Pen(salida ? _energizado : _texto, 2), arco);
            DibujarTexto(dc, Bobina ?? "", x, y - 32, AnchoBobina - 8, TextAlignment.Left, salida ? FontWeight.Bold : FontWeight.Normal);
        }

        private void LeerColores()
        {
            _texto = Foreground ?? Brushes.Black;
            _energizado = Recurso("App.Good", Brushes.Green);
            _cerrado = Recurso("App.GoodSoft", Brushes.LightGreen);
            _clave = Recurso("App.Warn", Brushes.DarkOrange);
            _claveFondo = Recurso("App.WarnSoft", Brushes.Moccasin);
            _hover = Recurso("App.AccentSoft", Brushes.LightBlue);
            _superficie = Recurso("App.Surface", Brushes.White);
        }

        private IBrush Recurso(string clave, IBrush otro) =>
            this.TryFindResource(clave, ActualThemeVariant, out object valor) && valor is IBrush brocha ? brocha : otro;

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
                dc.DrawRectangle(_claveFondo, new Pen(_clave, 1.5), zona, 4, 4);
            if (variable == _variableHover)
                dc.DrawRectangle(_hover, null, zona, 4, 4);

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
                         valor || esClave ? FontWeight.Bold : FontWeight.Normal,
                         esClave ? _clave : hayClaves ? Apagado : null);
            DibujarTexto(dc, valor ? "1" : "0", xb + 3, y + 2, 12, TextAlignment.Left, FontWeight.Normal, valor ? _energizado : Apagado);
        }

        private void DibujarCaja(DrawingContext dc, string texto, double xc, double y, bool salida)
        {
            var rect = new Rect(xc - AnchoContacto, y - 11, AnchoContacto * 2, 22);
            dc.DrawRectangle(salida ? _cerrado : _superficie, new Pen(salida ? _energizado : _texto, 1.5), rect);
            DibujarTexto(dc, texto, rect.X, y - 8, rect.Width, TextAlignment.Center, FontWeight.SemiBold);
        }

        private void DibujarCable(DrawingContext dc, double xDesde, double xHasta, double y, bool energizado)
        {
            if (xHasta > xDesde)
                dc.DrawLine(Lapiz(energizado), new Point(xDesde, y), new Point(xHasta, y));
        }

        private void DibujarTexto(DrawingContext dc, string texto, double x, double y, double ancho, TextAlignment alineacion,
                                  FontWeight peso, IBrush color = null)
        {
            var ft = new FormattedText(texto, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                                       new Typeface(FontFamily.Default, FontStyle.Normal, peso), 10.5, color ?? _texto)
            {
                MaxTextWidth = Math.Max(1, ancho),
                MaxLineCount = 2,
                Trimming = TextTrimming.CharacterEllipsis,
                TextAlignment = alineacion
            };
            dc.DrawText(ft, new Point(x, y));
        }

        private static string NombreVisible(string variable) => variable.Replace("\"", "");

        private static double YLinea(int fila) => fila * AltoCelda + AltoCelda - 16;

        private IPen Lapiz(bool energizado) => energizado ? new Pen(_energizado, 2.5) : new Pen(Apagado, 1.5);

        private IDictionary<string, bool> EstadoActual => Estado ?? new Dictionary<string, bool>();

        #endregion

        #region Interaction

        private string ContactoEn(Point p) => _contactos.FirstOrDefault(c => c.Zona.Contains(p)).Variable;

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            string variable = ContactoEn(e.GetPosition(this));
            if (variable == _variableHover)
                return;

            _variableHover = variable;
            Cursor = variable != null ? new Cursor(StandardCursorType.Hand) : null;
            ToolTip.SetTip(this, variable == null ? null
                : variable + " = " + (EstadoActual.TryGetValue(variable, out bool v) && v ? "1" : "0")
                  + (SenalesClave != null && SenalesClave.Contains(variable) ? "\n" + L.T("Señal que diferencia los dos pasos") : "")
                  + "\n" + L.T("Clic para cambiarla"));
            InvalidateVisual();
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            _variableHover = null;
            InvalidateVisual();
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (e.InitialPressMouseButton != MouseButton.Left)
                return;
            string variable = ContactoEn(e.GetPosition(this));
            if (variable != null && ToggleCommand?.CanExecute(variable) == true)
                ToggleCommand.Execute(variable);
        }

        #endregion
    }
}
