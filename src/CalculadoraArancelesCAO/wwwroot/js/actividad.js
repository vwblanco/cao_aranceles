// Registro de actividad del usuario para el bloqueo por inactividad.
// Llama a un método .NET cuando hay interacción real (toques, teclado, scroll),
// con un límite de frecuencia para no inundar el canal de interoperación.

export function registrar(dotNetRef, intervaloMs = 15000) {
    let ultimo = 0;
    let pendiente = false;

    const avisar = async () => {
        const ahora = Date.now();
        if (pendiente) {
            return;
        }

        const transcurrido = ahora - ultimo;
        if (transcurrido < intervaloMs) {
            pendiente = true;
            setTimeout(() => { pendiente = false; avisar(); }, intervaloMs - transcurrido);
            return;
        }

        ultimo = ahora;
        try {
            await dotNetRef.invokeMethodAsync('NotificarActividad');
        } catch (error) {
            // El circuito puede estar cerrado durante la navegacion: se ignora.
        }
    };

    const eventos = ['pointerdown', 'keydown', 'wheel', 'touchstart'];
    eventos.forEach(evento => document.addEventListener(evento, avisar, { passive: true }));

    return {
        desconectar() {
            eventos.forEach(evento => document.removeEventListener(evento, avisar));
        }
    };
}