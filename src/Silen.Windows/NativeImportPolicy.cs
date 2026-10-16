using System.Runtime.InteropServices;

// Las importaciones propias son bibliotecas de Windows. Nunca resolver desde la
// carpeta portable, el directorio actual o PATH, tampoco en el ayudante elevado.
// Esta política no se hereda a los ensamblados de sensores de terceros.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
