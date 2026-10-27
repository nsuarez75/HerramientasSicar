# Avisos de terceros

El código de HerramientasSICAR se publica con licencia MIT (ver [LICENSE](LICENSE)). El ejecutable incluye los componentes
de terceros de esta lista, cada uno con su licencia. Las herramientas que solo se usan para compilar o probar
(SDK de .NET, Fody, xUnit…) no van dentro del ejecutable y no se listan.

| Componente | Licencia | Titular | Enlace |
|---|---|---|---|
| ClosedXML | MIT | ClosedXML contributors | https://github.com/ClosedXML/ClosedXML |
| DocumentFormat.OpenXml (Open XML SDK) | MIT | Microsoft Corporation | https://github.com/dotnet/Open-XML-SDK |
| ExcelNumberFormat | MIT | andersnm | https://github.com/andersnm/ExcelNumberFormat |
| XLParser (dependencia de ClosedXML) | MPL-2.0 | Spreadsheet Lab (TU Delft) y colaboradores | https://github.com/spreadsheetlab/XLParser |
| Irony (Irony.NetCore, dependencia de XLParser) | MIT | Roman Ivantsov y colaboradores | https://github.com/daxnet/irony |
| SixLabors.Fonts | Apache-2.0 | Six Labors | https://github.com/SixLabors/Fonts |
| EPPlus 4.5.3.2 (sin modificar) | LGPL-3.0-or-later | Jan Källman | https://github.com/JanKallman/EPPlus |
| MaterialDesignThemes y MaterialDesignColors | MIT (los iconos vienen de Material Design Icons: https://pictogrammers.com/docs/general/license/) | MaterialDesignInXAML contributors | https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit |
| GTranslate | MIT | d4n3436 | https://github.com/d4n3436/GTranslate |
| CommunityToolkit.Mvvm | MIT | .NET Foundation and Contributors | https://github.com/CommunityToolkit/dotnet |
| Microsoft.Extensions.DependencyInjection, Microsoft.IO.RecyclableMemoryStream y Microsoft.Xaml.Behaviors.Wpf | MIT | Microsoft Corporation | https://github.com/dotnet/runtime |
| Costura (cargador de librerías embebidas) | MIT | Simon Cropp | https://github.com/Fody/Costura |
| Paquetes System.* y Microsoft.Bcl.AsyncInterfaces | MIT | .NET Foundation and Contributors / Microsoft Corporation | https://github.com/dotnet/runtime |

## Textos de las licencias

- **MIT**: el texto es el de abajo, con el titular de cada componente en la línea de copyright.
- **Apache-2.0**: [`licenses/Apache-2.0.txt`](licenses/Apache-2.0.txt).
- **MPL-2.0**: [`licenses/MPL-2.0.txt`](licenses/MPL-2.0.txt).
- **LGPL-3.0-or-later**: [`licenses/LGPL-3.0-or-later.txt`](licenses/LGPL-3.0-or-later.txt).
- **GPL-3.0-or-later**: [`licenses/GPL-3.0-or-later.txt`](licenses/GPL-3.0-or-later.txt).

EPPlus se usa sin modificar bajo LGPL-3.0 (que se apoya en la GPL-3.0, también incluida). Su código fuente está en
https://github.com/JanKallman/EPPlus. Va embebida en el ejecutable con Costura; quien quiera usar otra versión de
EPPlus puede compilar la app desde este repositorio cambiando la referencia del paquete.

## Siemens TIA Portal Openness

La app usa las librerías de TIA Portal Openness (`Siemens.Engineering.dll`), que son software propietario de
Siemens AG. **No se incluyen** ni en el repositorio ni en el ejecutable: se cargan de la instalación de TIA Portal
del propio equipo, y su uso se rige por la licencia de TIA Portal.

## Texto de la licencia MIT

```
MIT License

Copyright (c) <titular de cada componente, ver la tabla>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
