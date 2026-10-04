let eventoInstalacion = null;

window.addEventListener("beforeinstallprompt", function (evento) {
    evento.preventDefault();
    eventoInstalacion = evento;
});

export function instalar() {
    if (!eventoInstalacion) {
        return Promise.resolve(false);
    }

    eventoInstalacion.prompt();

    return eventoInstalacion.userChoice
        .then(function (eleccion) {
            eventoInstalacion = null;
            return eleccion.outcome === "accepted";
        });
}
