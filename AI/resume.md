# RESUMEN - GeminisPro (Formulario B-2 SICOES + UNDO/REDO)

## Objective
1. Formulario B-2 SICOES (PDF y Excel) con formato oficial, sin tocar CONSULTORIA. → COMPLETADO Y CONFIRMADO por el usuario.
2. Undo/Redo (Ctrl+Z / Ctrl+Y) en proyecto y APUs "y donde sea necesario", sin sobrecargar el programa. → En curso: **Fase 1 (EditorApu) completa y compilada (0 errores); Fase 2 (MainWindow/Proyecto) PENDIENTE**.

## Important Details
- (Contexto B-2 completo sigue vigente: build salida real, causa raíz flags SICOES, NOTA negra, títulos por ítem, filas MATERIALES, tamaños de fuente — ver historial del chat; archivos: ExcelReportEngine.cs / PdfReportEngine.cs / ReportPreviewWindow.xaml.cs).
- **Build salida real**: `dotnet build "...\Geminis.UI\Geminis.UI.csproj" -c Debug --nologo -v q 2>&1 | Select-String -Pattern "error CS|Errores"` → ejecutable `Geminis.UI\bin\Debug\net8.0-windows\Geminis.UI.exe`.
- No hay historial previo de edición en el programa: los POCOs (CE_Proyecto/CE_Item/CE_ItemDetalle) se mutan y se guardan con EF Core (GeminisDbContext, SQLite).

## UNDO/REDO — Estado actual
### Implementado (Fase 1 — Editor de APU)
- **Nuevo archivo**: `Geminis.Core\HistorialAcciones.cs` — historial reutilizable (stack deshacer/rehacer, límite 100 pasos, `Registrar(nombre, deshacer, rehacer)`, `Deshacer()`, `Rehacer()`, `Limpiar()`, evento `CambioDeEstado`, `PuedeDeshacer/PuedeRehacer`).
- **`EditorApu.xaml.cs`**:
  - Campo `_historial` (HistorialAcciones), `_ultimaCabecera`, `_rendimientoInicioEdicion`.
  - Cabecera (Código/Desc./Unidad/Grupo/Subgrupo): se registra undo en `LostFocus` (métodos `ObtenerCabecera/AplicarCabecera/Cabecera_LostFocus`); baseline al cargar (fin de `EditorApu_Loaded`).
  - Agregar insumo (`LogicaInsertarInsumoEnGrid`): undo remueve / redo re-inserta en el índice.
  - Reemplazar insumo (`MenuReemplazar_Click`): undo restaura Insumo/InsumoId/PrecioUnitarioFijado previos.
  - Editar nombre (`MenuEditarNombre_Click`) y precio (`MenuEditarPrecio_Click`): undo/redo de valores previos/nuevos.
  - Quitar insumo (`BtnQuitarInsumo_Click`): undo re-inserta en el índice, redo remueve.
  - Rendimiento en celda: `Dg_BeginningEdit` captura valor previo; `Dg_CellEditEnding` registra undo/redo (validación existente intacta).
  - Teclas: `Root_PreviewKeyDown` (Ctrl+Z=Deshacer, Ctrl+Y=Rehacer; se omite si se está editando una celda → deja el Ctrl+Z nativo de la celda). Conectado en `EditorApu.xaml` (raíz `PreviewKeyDown="Root_PreviewKeyDown"`).
  - `BtnSalir_Click` limpia el historial.
- Compiló con 0 errores a la salida real. **PENDIENTE: que el usuario pruebe la Fase 1.**

### Pendiente (Fase 2 — MainWindow/Proyecto)
- MainWindow es enorme (6448 líneas) y delicado: ~20 puntos de mutación del presupuesto (`_presupuestoActual.Add/Remove`), flujo de indirectos (`_matrizIndirectosActual` + fields `_csActual/_ivaMoActual/_herrActual/_ggActual/_utActual/_itActual`), confirmación de cambio de unidad (`ProcesarCambioUnidad`), cuadrilla que sincroniza cronograma, `RefrescarPresupuestoSeguro`.
- Plan acotado a proponer/al hacer: undo para ediciones en sitio de la grilla (Cantidad y Descripción del ítem vía BeginningEdit+CellEditEnding en `DgPresupuestoPrincipal_CellEditEnding` :612) y para la aplicación de indirectos desde `MatrizIndirectosWindow` (:1378). NO tocar agregar/quitar/mover ítems ni propagación de precios (riesgo alto).
- Puntos de referencia (cambian al editar): MainWindow.xaml.cs:612 (CellEditEnding del presupuesto), :671 `ProcesarCambioUnidad`, :1336-1390 (indirectos + MatrizIndirectosWindow), columna Cantidad :496, colUnidad :482, colCuadrilla :561.

## B-2 SICOES (ya confirmado y estable)
- 4 cambios del último pedido aplicados y verificados: (1) filas de MATERIALES extendidas (forzarFilasVacias + mínimo 14 SICOES), (2) título "FORMULARIO B-2 / ANALISIS DE PRECIOS UNITARIOS" por ítem en Excel (return temprano en AgregarEncabezado para FormularioB2), (3) NOTA negra en fila propia sin fila vacía siguiente (`fila++` antes y `rangoAPU` en `fila-1`), (4) tamaños de títulos de sección: Excel 11pt (SICOES) y PDF 10pt (bandas SICOES; consultoría intacta 8pt).

## Restricciones vigentes
- No modificar acciones innecesarias ni el formato CONSULTORIA.
- No usar binarios de `Temp\opencode\ui-build2` (viejos).

## Relevant Files
- `Geminis.Core\HistorialAcciones.cs` (NUEVO — infraestructura undo/redo).
- `Geminis.UI\EditorApu.xaml.cs` (Fase 1 integrada), `EditorApu.xaml` (PreviewKeyDown).
- `Geminis.UI\MainWindow.xaml.cs` / `MainWindow.xaml` (Fase 2 pendiente).
- `Geminis.UI\Reportes\ReportGenerators\ExcelReportEngine.cs` / `PdfReportEngine.cs` / `ReportPreviewWindow.xaml.cs` (B-2 SICOES, estable).
- `Geminis.Core\CE_Proyecto.cs`, `CE_Item`, `CE_ItemDetalle` (modelo).
- Ejecutable real: `Geminis.UI\bin\Debug\net8.0-windows\Geminis.UI.exe`.
- Backups: `E:\Programacion\Programas C\2026\GeminisPro\GeminisPro Backup 2026-09-08\` y `H:\Datos\GeminisPro Backup 2026-09-08\` (verificados).

## Próximo paso
- Que el usuario pruebe la Fase 1 (EditorApu: agregar/quitar/reemplazar insumo, editar rendimiento/precio/nombre, campos de cabecera; Ctrl+Z/Ctrl+Y).
- Luego continuar Fase 2 acotada en MainWindow (cantidad/descripción de ítems + indirectos).