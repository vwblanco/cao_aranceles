window.caoPdf = (function () {
    const configuracion = {
        margin: [10, 10, 12, 10],
        image: { type: "jpeg", quality: 0.98 },
        html2canvas: {
            scale: 2,
            useCORS: true,
            backgroundColor: "#ffffff",
            letterRendering: true,
            windowWidth: 800,
            windowHeight: document.body.scrollHeight,
            scrollX: 0,
            scrollY: 0
        },
        jsPDF: { unit: "mm", format: "letter", orientation: "portrait" },
        pagebreak: { mode: ["avoid-all", "css", "legacy"], avoid: ["tr", ".firma", ".legal", ".total", ".parametros"] }
    };

    function disponible() {
        return typeof window.html2pdf !== "undefined";
    }

    function generar(html, nombreArchivo) {
        if (!disponible()) {
            return Promise.reject(new Error("html2pdf.js no está disponible en este dispositivo."));
        }

        const contenedor = document.createElement("div");
        // Posición absoluta fuera de pantalla, pero visible para html2canvas
        contenedor.style.position = "absolute";
        contenedor.style.left = "-9999px";
        contenedor.style.top = "0";
        contenedor.style.width = "800px";
        contenedor.style.minHeight = "100vh";  // forzar alto mínimo
        contenedor.style.zIndex = "-1";
        contenedor.style.opacity = "1";        // visible para html2canvas
        contenedor.style.pointerEvents = "none";
        contenedor.innerHTML = html;
        document.body.appendChild(contenedor);

        // Forzar layout y esperar TODAS las imágenes
        return new Promise(function (resolve, reject) {
            const imagenes = Array.from(contenedor.querySelectorAll('img'));
            const promesasImg = imagenes.map(img => {
                if (img.complete) return Promise.resolve();
                return new Promise(r => { img.onload = img.onerror = r; });
            });

            Promise.all(promesasImg).then(function () {
                // Forzar reflow
                contenedor.offsetHeight;

                const opciones = Object.assign({}, configuracion, { filename: nombreArchivo });

                window.html2pdf()
                    .set(opciones)
                    .from(contenedor)
                    .toPdf()
                    .get('pdf')
                    .then(function (pdf) {
                        var totalPages = pdf.internal.getNumberOfPages();
                        for (var i = 1; i <= totalPages; i++) {
                            pdf.setPage(i);
                        }
                        var blob = pdf.output('blob');
                        console.log('PDF generado:', blob.size, 'bytes, páginas:', totalPages);
                        if (blob.size < 5000) {
                            console.warn('PDF muy pequeño, posible página en blanco');
                        }
                        document.body.removeChild(contenedor);
                        return blob;
                    })
                    .then(function (blob) {
                        var url = URL.createObjectURL(blob);
                        var a = document.createElement('a');
                        a.href = url;
                        a.download = nombreArchivo;
                        document.body.appendChild(a);
                        a.click();
                        document.body.removeChild(a);
                        URL.revokeObjectURL(url);
                        resolve(true);
                    })
                    .catch(function (error) {
                        document.body.removeChild(contenedor);
                        reject(error);
                    });
            });
        });
    }

    function imprimir(html) {
        const ventana = window.open("", "_blank", "width=900,height=1100");
        if (!ventana) {
            return Promise.reject(new Error("El navegador bloqueó la ventana de impresión."));
        }
        // Asegurar que las rutas de imágenes sean absolutas
        const base = window.location.origin + window.location.pathname.substring(0, window.location.pathname.lastIndexOf('/') + 1);
        const htmlConBase = html.replace(/src="CAO-192\.png"/g, `src="${base}CAO-192.png"`);
        ventana.document.write(htmlConBase);
        ventana.document.close();
        ventana.focus();
        ventana.print();
        return Promise.resolve(true);
    }

    function verificarArchivo(ruta) {
        return fetch(ruta, { method: 'HEAD', cache: 'no-cache' })
            .then(r => r.ok)
            .catch(() => false);
    }

    function descargarArchivo(ruta, nombre) {
        return fetch(ruta)
            .then(r => r.blob())
            .then(blob => {
                const url = URL.createObjectURL(blob);
                const a = document.createElement('a');
                a.href = url;
                a.download = nombre;
                document.body.appendChild(a);
                a.click();
                document.body.removeChild(a);
                URL.revokeObjectURL(url);
            });
    }

    function imprimirArchivo(ruta) {
        const ventana = window.open(ruta, '_blank');
        if (ventana) {
            ventana.onload = () => ventana.print();
        } else {
            alert('El navegador bloqueó la ventana. Permite ventanas emergentes para este sitio.');
        }
    }

    function compartirArchivo(nombre, tipo, html) {
        return new Promise(function (resolve, reject) {
            if (navigator.share && navigator.canShare && navigator.canShare({ files: [] })) {
                // Generar PDF blob primero
                window.caoPdf.generar(html, nombre).then(function (blob) {
                    var archivo = new File([blob], nombre, { type: 'application/pdf' });
                    navigator.share({
                        title: 'Cotización CAO',
                        text: 'Cotización CAO',
                        files: [archivo]
                    }).then(function () {
                        resolve(true);
                    }).catch(function (err) {
                        // Usuario canceló o error
                        resolve(false);
                    });
                }).catch(function () {
                    resolve(false);
                });
            } else {
                resolve(false);
            }
        });
    }

    function compartirTexto(titulo, texto, url) {
        return new Promise(function (resolve, reject) {
            if (navigator.share) {
                navigator.share({
                    title: titulo,
                    text: texto,
                    url: url
                }).then(function () {
                    resolve(true);
                }).catch(function () {
                    resolve(false);
                });
            } else {
                resolve(false);
            }
        });
    }

    return {
        generar: generar,
        imprimir: imprimir,
        disponible: disponible,
        verificarArchivo: verificarArchivo,
        descargarArchivo: descargarArchivo,
        imprimirArchivo: imprimirArchivo,
        compartirArchivo: compartirArchivo,
        compartirTexto: compartirTexto
    };
})();