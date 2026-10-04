Attribute VB_Name = "DocumentSplitter"
Option Explicit

' Divide el documento por Título 1 y por saltos de página manuales.
' El contenido se copia con FormattedText para conservar el formato de Word.
Public Sub DividirDocumento()
    Dim origen As Document
    Dim carpeta As String
    Dim secciones As Collection
    Dim pantallaAnterior As Boolean
    Dim i As Long
    Dim creados As Long

    If Documents.Count = 0 Then
        MsgBox "No hay ningún documento abierto.", vbExclamation, "Divisor de documentos"
        Exit Sub
    End If

    Set origen = ActiveDocument

    If origen.ProtectionType <> wdNoProtection Then
        MsgBox "El documento está protegido y no puede procesarse.", vbExclamation, "Divisor de documentos"
        Exit Sub
    End If

    carpeta = SeleccionarCarpeta(origen.Path)
    If Len(carpeta) = 0 Then Exit Sub

    Set secciones = DetectarSecciones(origen)

    If secciones.Count = 0 Then
        MsgBox "No se encontraron títulos Título 1 ni saltos de página utilizables.", vbInformation, "Divisor de documentos"
        Exit Sub
    End If

    If MsgBox("Se detectaron " & secciones.Count & " secciones." & vbCrLf & _
              "Se creará un archivo .docx por sección." & vbCrLf & vbCrLf & _
              "¿Continuar?", vbQuestion + vbYesNo, "Divisor de documentos") <> vbYes Then Exit Sub

    pantallaAnterior = Application.ScreenUpdating
    Application.ScreenUpdating = False
    On Error GoTo ErrorProceso

    For i = 1 To secciones.Count
        Application.StatusBar = "Creando archivo " & i & " de " & secciones.Count & "..."
        CrearArchivo origen, secciones(i), carpeta
        creados = creados + 1
    Next i

    Application.StatusBar = False
    Application.ScreenUpdating = pantallaAnterior

    MsgBox "Proceso terminado." & vbCrLf & _
           "Archivos creados: " & creados & vbCrLf & _
           "Carpeta: " & carpeta, vbInformation, "Divisor de documentos"
    Exit Sub

ErrorProceso:
    Application.StatusBar = False
    Application.ScreenUpdating = pantallaAnterior
    MsgBox "El proceso se detuvo después de crear " & creados & " archivo(s)." & vbCrLf & _
           "Error: " & Err.Description, vbCritical, "Divisor de documentos"
End Sub

' Compatibilidad con el nombre de la macro anterior.
Public Sub DividirPorTitulo1()
    DividirDocumento
End Sub

' Cada elemento contiene: título, posición inicial y posición final.
Private Function DetectarSecciones(ByVal origen As Document) As Collection
    Dim resultado As New Collection
    Dim parrafo As Paragraph
    Dim titulos As Collection
    Dim saltos As Collection
    Dim item As Variant
    Dim inicio As Long
    Dim fin As Long
    Dim titulo As String
    Dim texto As String
    Dim indiceTitulo As Long
    Dim anterior As Long
    Dim salto As Variant

    Set titulos = New Collection
    Set saltos = New Collection

    For Each parrafo In origen.Paragraphs
        If EsTitulo1(parrafo) Then
            titulo = TextoLimpio(parrafo.Range.Text)
            If Len(titulo) > 0 Then
                titulos.Add Array(CLng(parrafo.Range.Start), titulo)
            End If
        End If

        ' Chr(12) representa un salto de página manual dentro del texto de Word.
        ' También se contempla PageBreakBefore, que no siempre aparece como Chr(12).
        If InStr(1, parrafo.Range.Text, Chr$(12), vbBinaryCompare) > 0 Then
            saltos.Add CLng(parrafo.Range.End)
        ElseIf TieneSaltoAntes(parrafo) Then
            saltos.Add CLng(parrafo.Range.Start)
        End If
    Next parrafo

    ' Prioridad: si Word reconoce Título 1, cada Título 1 define una sección.
    ' Así el salto de página final no puede reemplazar el nombre ni excluir el título.
    If titulos.Count > 0 Then
        For indiceTitulo = 1 To titulos.Count
            item = titulos(indiceTitulo)
            inicio = CLng(item(0))
            titulo = CStr(item(1))
            If indiceTitulo < titulos.Count Then
                item = titulos(indiceTitulo + 1)
                fin = CLng(item(0))
            Else
                fin = origen.Content.End - 1
            End If

            texto = TextoLimpio(origen.Range(inicio, fin).Text)
            If Len(texto) > 0 Then resultado.Add Array(titulo, inicio, fin)
        Next indiceTitulo
    ElseIf saltos.Count > 0 Then
        ' Fallback: si no hay Título 1, cada salto de página delimita una sección.
        anterior = 0

        For Each salto In saltos
            fin = CLng(salto)
            If fin > anterior Then
                titulo = PrimerTexto(origen.Range(anterior, fin).Text)
                If Len(titulo) = 0 Then titulo = "Seccion_" & Format$(resultado.Count + 1, "00")
                texto = TextoLimpio(origen.Range(anterior, fin).Text)
                If Len(texto) > 0 Then resultado.Add Array(titulo, anterior, fin)
            End If
            anterior = fin
        Next salto

        fin = origen.Content.End - 1
        If fin > anterior Then
            titulo = PrimerTexto(origen.Range(anterior, fin).Text)
            If Len(titulo) = 0 Then titulo = "Seccion_" & Format$(resultado.Count + 1, "00")
            texto = TextoLimpio(origen.Range(anterior, fin).Text)
            If Len(texto) > 0 Then resultado.Add Array(titulo, anterior, fin)
        End If
    End If

    Set DetectarSecciones = resultado
End Function

Private Sub AgregarCandidato(ByVal candidatos As Collection, ByVal posicion As Long, ByVal titulo As String)
    Dim item(1 To 2) As Variant
    item(1) = posicion
    item(2) = titulo
    candidatos.Add item
End Sub

Private Sub CrearArchivo(ByVal origen As Document, ByVal seccion As Variant, ByVal carpeta As String)
    Dim rango As Range
    Dim nuevo As Document
    Dim titulo As String
    Dim ruta As String

    titulo = CStr(seccion(0))
    Set rango = origen.Range(Start:=CLng(seccion(1)), End:=CLng(seccion(2)))
    If Len(TextoLimpio(rango.Text)) = 0 Then Exit Sub

    ruta = RutaDisponible(carpeta, LimpiarNombre(titulo), ".docx")
    Set nuevo = Documents.Add

    ' Copia nativa de Word: conserva formato, tablas, imágenes, campos,
    ' listas, saltos y propiedades de párrafo contenidos en el rango.
    nuevo.Range(Start:=0, End:=0).FormattedText = rango.FormattedText

    On Error Resume Next
    nuevo.BuiltInDocumentProperties(wdPropertyTitle).Value = titulo
    On Error GoTo 0

    nuevo.SaveAs2 FileName:=ruta, FileFormat:=wdFormatXMLDocument
    nuevo.Close SaveChanges:=wdDoNotSaveChanges
End Sub

Private Function EsTitulo1(ByVal parrafo As Paragraph) As Boolean
    Dim nombre As String

    On Error Resume Next
    If parrafo.Style = wdStyleHeading1 Then
        EsTitulo1 = True
        Exit Function
    End If
    nombre = CStr(parrafo.Style)
    On Error GoTo 0

    nombre = Normalizar(nombre)
    EsTitulo1 = (nombre = "titulo1" Or nombre = "heading1")
End Function

Private Function TieneSaltoAntes(ByVal parrafo As Paragraph) As Boolean
    On Error Resume Next
    TieneSaltoAntes = (parrafo.PageBreakBefore = True)
    On Error GoTo 0
End Function

Private Function PrimerTexto(ByVal texto As String) As String
    Dim lineas As Variant
    Dim linea As Variant

    texto = Replace(texto, Chr$(12), vbCr)
    lineas = Split(texto, vbCr)
    For Each linea In lineas
        If Len(TextoLimpio(CStr(linea))) > 0 Then
            PrimerTexto = Left$(LimpiarNombre(TextoLimpio(CStr(linea))), 120)
            Exit Function
        End If
    Next linea
End Function

Private Function SeleccionarCarpeta(ByVal inicial As String) As String
    Dim dialogo As FileDialog
    Set dialogo = Application.FileDialog(4) ' msoFileDialogFolderPicker

    With dialogo
        .Title = "Seleccione la carpeta de destino"
        .AllowMultiSelect = False
        If Len(inicial) > 0 Then .InitialFileName = inicial
        If .Show = -1 Then SeleccionarCarpeta = .SelectedItems(1)
    End With
End Function

Private Function TextoLimpio(ByVal texto As String) As String
    texto = Replace(texto, vbCr, "")
    texto = Replace(texto, Chr$(7), "")
    texto = Replace(texto, Chr$(12), "")
    TextoLimpio = Trim$(texto)
End Function

Private Function Normalizar(ByVal texto As String) As String
    texto = LCase$(Trim$(texto))
    texto = Replace(texto, "í", "i")
    texto = Replace(texto, "é", "e")
    texto = Replace(texto, " ", "")
    Normalizar = texto
End Function

Private Function LimpiarNombre(ByVal nombre As String) As String
    Dim invalidos As Variant
    Dim caracter As Variant

    invalidos = Array("\", "/", ":", "*", "?", Chr$(34), "<", ">", "|")
    For Each caracter In invalidos
        nombre = Replace(nombre, CStr(caracter), "_")
    Next caracter

    nombre = Replace(nombre, vbCr, " ")
    nombre = Replace(nombre, vbLf, " ")
    nombre = Replace(nombre, Chr$(12), " ")
    nombre = Trim$(nombre)

    Do While InStr(nombre, "  ") > 0
        nombre = Replace(nombre, "  ", " ")
    Loop

    If Len(nombre) = 0 Then nombre = "Seccion"
    If Len(nombre) > 120 Then nombre = Left$(nombre, 120)
    LimpiarNombre = nombre
End Function

Private Function RutaDisponible(ByVal carpeta As String, ByVal nombre As String, _
                                ByVal extension As String) As String
    Dim ruta As String
    Dim contador As Long

    If Right$(carpeta, 1) <> "\" Then carpeta = carpeta & "\"
    ruta = carpeta & nombre & extension
    contador = 2

    Do While Len(Dir$(ruta)) > 0
        ruta = carpeta & nombre & " (" & contador & ")" & extension
        contador = contador + 1
    Loop

    RutaDisponible = ruta
End Function
