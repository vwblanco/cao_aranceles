# Instalación desde cero en Word

La versión actual divide por **Título 1** y contempla los **saltos de página manuales**. Cuando existen Títulos 1, estos tienen prioridad: el salto de página se conserva dentro del rango y no puede quitar el título ni cambiar el nombre del archivo. Si no existe ningún Título 1 reconocible, los saltos de página se usan como delimitadores alternativos.

## 1. Guardar el código

El código está en:

```text
vba\DocumentSplitter.bas
```

## 2. Abrir el editor VBA

En Word:

1. Abra Word de escritorio.
2. Presione `Alt + F11`.
3. En el menú del editor seleccione **Archivo > Importar archivo**.
4. Seleccione `vba\DocumentSplitter.bas`.

También puede crear un módulo manualmente con **Insertar > Módulo** y pegar el contenido del archivo.

## 3. Guardar la macro

Para que esté disponible en todos los documentos:

1. En el panel izquierdo del editor, seleccione `Normal (Normal.dotm)`.
2. Importe allí `DocumentSplitter.bas`.
3. Presione `Ctrl + S` dentro del editor.
4. Cierre Word y acepte guardar `Normal.dotm` si lo solicita.

Si solo quiere usarla en un documento, importe el módulo en el proyecto VBA de ese documento y guárdelo como `.docm`.

## 4. Habilitar macros

Si Word bloquea la macro:

1. Abra **Archivo > Opciones**.
2. Seleccione **Centro de confianza > Configuración del Centro de confianza**.
3. Entre en **Configuración de macros**.
4. Seleccione **Deshabilitar todas las macros con notificación**.
5. Reinicie Word y habilite el contenido cuando aparezca la barra amarilla.

## 5. Ejecutar la aplicación

1. Abra el documento general.
2. Verifique que las secciones usen el estilo **Título 1**.
3. Presione `Alt + F8`.
4. Seleccione `DividirDocumento`.
5. Pulse **Ejecutar**.
6. Seleccione la carpeta de destino.

Se creará un archivo `.docx` por cada Título 1 o sección delimitada por salto de página. Cuando una sección no tiene Título 1, se usa su primer texto no vacío como nombre:

```text
Introducción.docx
Metodología.docx
Resultados.docx
```

## 6. Agregar al ribbon

No es necesario editar XML:

1. Abra **Archivo > Opciones > Personalizar cinta de opciones**.
2. Pulse **Nueva pestaña** y cambie su nombre a `Dividir documento`.
3. En **Comandos disponibles en**, seleccione **Macros**.
4. Seleccione `Normal.Module1.DividirPorTitulo1` o `Normal.DocumentSplitter.DividirPorTitulo1`.
5. Pulse **Agregar** y luego **Aceptar**.

El botón quedará disponible en el ribbon y ejecutará la macro.

## Qué conserva

El código copia el rango con `FormattedText`, usando Word directamente. Esto conserva el formato de caracteres y párrafos, tablas, imágenes, campos, saltos de sección y listas incluidos dentro de cada sección.

## Alcance

El archivo se divide desde cada párrafo **Título 1** o salto de página hasta el siguiente delimitador. Los saltos manuales se detectan como `Ctrl+Enter` y como la propiedad **Salto de página anterior** del párrafo.
