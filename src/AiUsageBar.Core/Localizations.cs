namespace AiUsageBar.Core;

/// <summary>
/// Translations in the order: Simplified Chinese, Spanish, Brazilian Portuguese, German.
/// Keys are the English source strings passed to UiText.T / UiText.TFormat.
/// </summary>
internal static class Localizations
{
    public static readonly IReadOnlyDictionary<string, string[]> Text =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Sign in"] = ["登录", "Iniciar sesión", "Entrar", "Anmelden"],
            ["Rate limit"] = ["速率限制", "Límite de velocidad", "Limite de taxa", "Ratenlimit"],
            ["Failed"] = ["失败", "Error", "Falhou", "Fehlgeschlagen"],
            ["Unavailable"] = ["不可用", "No disponible", "Indisponível", "Nicht verfügbar"],
            ["Unlimited"] = ["无限制", "Ilimitado", "Ilimitado", "Unbegrenzt"],
            ["Unknown"] = ["未知", "Desconocido", "Desconhecido", "Unbekannt"],
            ["soon"] = ["即将", "pronto", "em breve", "bald"],
            ["Plan"] = ["套餐", "Plan", "Plano", "Tarif"],
            ["On-demand"] = ["按需", "Bajo demanda", "Sob demanda", "Bedarfsbasiert"],
            ["Billing cycle"] = ["计费周期", "Ciclo de facturación", "Ciclo de cobrança", "Abrechnungszyklus"],
            ["Updated"] = ["更新时间", "Actualizado", "Atualizado", "Aktualisiert"],
            ["left"] = ["剩余", "restante", "restante", "übrig"],
            ["resets"] = ["重置", "se restablece", "reinicia", "Zurücksetzen"],
            ["available"] = ["可用", "disponible", "disponível", "verfügbar"],
            ["Extra usage"] = ["额外用量", "Uso adicional", "Uso extra", "Zusätzliche Nutzung"],
            ["Cloud minutes"] = ["云端分钟", "Minutos en la nube", "Minutos na nuvem", "Cloud-Minuten"],
            ["Total used"] = ["累计使用", "Total usado", "Total usado", "Insgesamt verwendet"],
            ["Click for details"] = ["点击查看详情", "Haz clic para ver detalles", "Clique para ver detalhes", "Klicken für Details"],
            ["No providers selected"] = ["未选择提供商", "No hay proveedores seleccionados", "Nenhum provedor selecionado", "Keine Anbieter ausgewählt"],
            ["Console cookie (optional)"] = ["控制台 Cookie（可选）", "Cookie de consola (opcional)", "Cookie do console (opcional)", "Konsolen-Cookie (optional)"],

            ["Refresh now(&R)"] = ["立即刷新(&R)", "Actualizar ahora(&R)", "Atualizar agora(&R)", "Jetzt aktualisieren(&R)"],
            ["Settings(&S)"] = ["设置(&S)", "Configuración(&S)", "Configurações(&S)", "Einstellungen(&S)"],
            ["Exit(&X)"] = ["退出(&X)", "Salir(&X)", "Sair(&X)", "Beenden(&X)"],

            ["Settings"] = ["设置", "Configuración", "Configurações", "Einstellungen"],
            ["Display and refresh"] = ["显示与刷新", "Visualización y actualización", "Exibição e atualização", "Anzeige und Aktualisierung"],
            ["General"] = ["常规", "General", "Geral", "Allgemein"],
            ["Language"] = ["语言", "Idioma", "Idioma", "Sprache"],
            ["Match Windows"] = ["跟随 Windows", "Usar idioma de Windows", "Usar o Windows", "Windows-Einstellung verwenden"],
            ["Refresh interval (seconds)"] = ["刷新间隔（秒）", "Intervalo de actualización (segundos)", "Intervalo de atualização (segundos)", "Aktualisierungsintervall (Sekunden)"],
            ["Providers"] = ["提供商", "Proveedores", "Provedores", "Anbieter"],
            ["Move up"] = ["上移", "Subir", "Mover para cima", "Nach oben"],
            ["Move down"] = ["下移", "Bajar", "Mover para baixo", "Nach unten"],
            ["Start with Windows"] = ["随 Windows 启动", "Iniciar con Windows", "Iniciar com o Windows", "Mit Windows starten"],
            ["Position"] = ["位置", "Posición", "Posição", "Position"],
            ["Offset X"] = ["X 偏移", "Desplazamiento X", "Deslocamento X", "X-Versatz"],
            ["Offset Y"] = ["Y 偏移", "Desplazamiento Y", "Deslocamento Y", "Y-Versatz"],

            ["Session token (optional)"] = ["会话令牌（可选）", "Token de sesión (opcional)", "Token de sessão (opcional)", "Sitzungstoken (optional)"],
            ["Paste the cursor.com cookie WorkosCursorSessionToken. Leave empty if the local Cursor app can supply it."] =
                ["粘贴 cursor.com 的 Cookie「WorkosCursorSessionToken」。如果本机 Cursor 应用能提供，可留空。",
                 "Pega la cookie WorkosCursorSessionToken de cursor.com. Déjalo vacío si la aplicación local de Cursor puede proporcionarla.",
                 "Cole o cookie WorkosCursorSessionToken do cursor.com. Deixe em branco se o aplicativo Cursor local puder fornecê-lo.",
                 "Füge das Cookie WorkosCursorSessionToken von cursor.com ein. Leer lassen, wenn die lokale Cursor-App es bereitstellen kann."],
            ["Codex sign-in"] = ["Codex 登录", "Inicio de sesión de Codex", "Login do Codex", "Codex-Anmeldung"],
            ["Uses the auth.json from your usual codex login. If it expires, sign in again with the button below."] =
                ["使用平时 codex login 的 auth.json。过期时请用下方按钮重新登录。",
                 "Usa el auth.json de tu inicio de sesión habitual de codex. Si caduca, vuelve a iniciar sesión con el botón de abajo.",
                 "Usa o auth.json do seu login habitual do codex. Se expirar, entre novamente com o botão abaixo.",
                 "Verwendet die auth.json aus deiner üblichen codex-Anmeldung. Bei Ablauf bitte über die Schaltfläche unten erneut anmelden."],
            ["Open codex login"] = ["打开 codex 登录", "Abrir inicio de sesión de codex", "Abrir login do codex", "codex-Anmeldung öffnen"],
            ["Remove saved token"] = ["删除已保存的令牌", "Eliminar token guardado", "Remover token salvo", "Gespeichertes Token entfernen"],
            ["Cancel"] = ["取消", "Cancelar", "Cancelar", "Abbrechen"],
            ["Save"] = ["保存", "Guardar", "Salvar", "Speichern"],

            ["Sign-in"] = ["登录", "Inicio de sesión", "Login", "Anmeldung"],
            ["OAuth token (optional)"] = ["OAuth 令牌（可选）", "Token OAuth (opcional)", "Token OAuth (opcional)", "OAuth-Token (optional)"],
            ["API key (optional)"] = ["API 密钥（可选）", "Clave de API (opcional)", "Chave de API (opcional)", "API-Schlüssel (optional)"],
            ["Usage URL"] = ["使用量 URL", "URL de uso", "URL de uso", "Nutzungs-URL"],
            ["Leave empty for the built-in URL. Set it when you know the endpoint your tool exposes."] =
                ["留空则使用内置 URL。只有在你确定获取地址时才填写。",
                 "Déjalo vacío para usar la URL integrada. Configúralo solo si conoces el endpoint que expone tu herramienta.",
                 "Deixe em branco para usar a URL integrada. Defina apenas se souber o endpoint exposto pela sua ferramenta.",
                 "Leer lassen für die integrierte URL. Nur setzen, wenn der Endpoint deines Tools bekannt ist."],
            ["Workspace (optional)"] = ["工作区（可选）", "Espacio de trabajo (opcional)", "Workspace (opcional)", "Arbeitsbereich (optional)"],
            ["Leave empty to detect it from the console sign-in. Paste the URL of your Go page (…/console/wrk_…/go) or the workspace ID when you use several workspaces."] =
                ["留空则根据 console 登录信息自动判断。使用多个工作区时，请粘贴 Go 页面的 URL（…/console/wrk_…/go）或工作区 ID。",
                 "Déjalo vacío para detectarlo desde el inicio de sesión de la consola. Si usas varios espacios de trabajo, pega la URL de tu página Go (…/console/wrk_…/go) o el ID del espacio de trabajo.",
                 "Deixe em branco para detectar pelo login do console. Se usar vários workspaces, cole a URL da sua página Go (…/console/wrk_…/go) ou o ID do workspace.",
                 "Leer lassen, um es aus der Konsolenanmeldung zu ermitteln. Bei mehreren Arbeitsbereichen die URL deiner Go-Seite (…/console/wrk_…/go) oder die Arbeitsbereich-ID einfügen."],

            ["Uses ~/.claude/.credentials.json from Claude Code. Paste the token only when you sign in elsewhere."] =
                ["使用 Claude Code 的 ~/.claude/.credentials.json。只有在别处登录时才需要粘贴令牌。",
                 "Usa ~/.claude/.credentials.json de Claude Code. Pega el token solo si inicias sesión en otro lugar.",
                 "Usa o ~/.claude/.credentials.json do Claude Code. Cole o token apenas se entrar em outro lugar.",
                 "Verwendet ~/.claude/.credentials.json von Claude Code. Token nur einfügen, wenn du dich anderswo anmeldest."],
            ["Sign in with the button below to get a console session. The Go limits come from the workspace selected in the console. If sign-in fails, paste the console session cookie (DevTools → Network → Request Headers → Cookie)."] =
                ["点击下方按钮获取 console 会话。Go 限额来自 console 中选择的工作区。如果登录失败，请粘贴 console 会话 Cookie（DevTools → Network → Request Headers → Cookie）。",
                 "Inicia sesión con el botón de abajo para obtener una sesión de la consola. Los límites de Go vienen del espacio de trabajo seleccionado en la consola. Si falla, pega la cookie de sesión de la consola (DevTools → Network → Request Headers → Cookie).",
                 "Entre com o botão abaixo para obter uma sessão do console. Os limites do Go vêm do workspace selecionado no console. Se falhar, cole o cookie de sessão do console (DevTools → Network → Request Headers → Cookie).",
                 "Über die Schaltfläche unten anmelden, um eine Konsolensitzung zu erhalten. Die Go-Limits stammen aus dem in der Konsole gewählten Arbeitsbereich. Bei Problemen das Konsolen-Sitzungscookie einfügen (DevTools → Network → Request Headers → Cookie)."],
            ["Uses ~/.commandcode/auth.json from commandcode login. Paste an API key when you have not signed in on this PC."] =
                ["使用 commandcode login 的 ~/.commandcode/auth.json。如果此 PC 未登录，请粘贴 API 密钥。",
                 "Usa ~/.commandcode/auth.json de commandcode login. Pega una clave de API si no has iniciado sesión en este PC.",
                 "Usa o ~/.commandcode/auth.json do commandcode login. Cole uma chave de API se não tiver entrado neste PC.",
                 "Verwendet ~/.commandcode/auth.json aus commandcode login. API-Schlüssel einfügen, wenn du dich auf diesem PC nicht angemeldet hast."],

            ["Sign in with Claude Code, or paste the OAuth token in Settings."] =
                ["使用 Claude Code 登录，或在设置中粘贴 OAuth 令牌。",
                 "Inicia sesión con Claude Code o pega el token OAuth en Configuración.",
                 "Entre com o Claude Code ou cole o token OAuth nas Configurações.",
                 "Mit Claude Code anmelden oder das OAuth-Token in den Einstellungen einfügen."],
            ["Sign in to the opencode console, then paste the console cookie in Settings."] =
                ["登录 opencode console，然后在设置中粘贴控制台 Cookie。",
                 "Inicia sesión en la consola de opencode y pega la cookie de la consola en Configuración.",
                 "Entre no console do opencode e cole o cookie do console nas Configurações.",
                 "Bei der opencode-Konsole anmelden und dann das Konsolen-Cookie in den Einstellungen einfügen."],
            ["Run commandcode login, or paste the API key in Settings."] =
                ["运行 commandcode login，或在设置中粘贴 API 密钥。",
                 "Ejecuta commandcode login o pega la clave de API en Configuración.",
                 "Execute commandcode login ou cole a chave de API nas Configurações.",
                 "commandcode login ausführen oder den API-Schlüssel in den Einstellungen einfügen."],
            ["Could not fetch usage. Check the usage URL and API key in Settings."] =
                ["获取使用量失败。请检查设置中的使用量 URL 和 API 密钥。",
                 "No se pudo obtener el uso. Revisa la URL de uso y la clave de API en Configuración.",
                 "Não foi possível obter o uso. Verifique a URL de uso e a chave de API nas Configurações.",
                 "Nutzung konnte nicht abgerufen werden. Prüfe Nutzungs-URL und API-Schlüssel in den Einstellungen."],
            ["The usage URL does not exist. Set the real endpoint in Settings."] =
                ["使用量 URL 不存在。请在设置中指定实际的获取地址。",
                 "La URL de uso no existe. Configura el endpoint real en Configuración.",
                 "A URL de uso não existe. Defina o endpoint real nas Configurações.",
                 "Die Nutzungs-URL existiert nicht. Den tatsächlichen Endpoint in den Einstellungen festlegen."],
            ["Sign in to opencode"] = ["登录 opencode", "Iniciar sesión en opencode", "Entrar no opencode", "Bei opencode anmelden"],
            ["Signed in to the opencode console."] =
                ["已登录 opencode console。", "Sesión iniciada en la consola de opencode.", "Login no console do opencode concluído.", "Bei der opencode-Konsole angemeldet."],
            ["Could not sign in to the opencode console."] =
                ["无法登录 opencode console。", "No se pudo iniciar sesión en la consola de opencode.", "Não foi possível entrar no console do opencode.", "Anmeldung bei der opencode-Konsole fehlgeschlagen."],

            ["AI Usage Bar is already running."] =
                ["AI Usage Bar 已在运行。", "AI Usage Bar ya se está ejecutando.", "O AI Usage Bar já está em execução.", "AI Usage Bar läuft bereits."],
            ["Could not open Settings."] =
                ["无法打开设置。", "No se pudo abrir Configuración.", "Não foi possível abrir as Configurações.", "Einstellungen konnten nicht geöffnet werden."],
            ["Could not add or remove the startup shortcut."] =
                ["无法添加或删除启动快捷方式。", "No se pudo añadir o quitar el acceso directo de inicio.", "Não foi possível adicionar ou remover o atalho de inicialização.", "Startverknüpfung konnte nicht hinzugefügt oder entfernt werden."],
            ["codex CLI was not found. Add it to PATH and try again."] =
                ["未找到 codex CLI。请将其添加到 PATH 后重试。", "No se encontró la CLI de codex. Añádela al PATH e inténtalo de nuevo.", "A CLI do codex não foi encontrada. Adicione-a ao PATH e tente novamente.", "codex CLI wurde nicht gefunden. Füge es dem PATH hinzu und versuche es erneut."],
            ["Sign in to Codex in the browser, then right-click the bar and choose Refresh now."] =
                ["在浏览器中登录 Codex，然后右键点击任务栏栏并选择「立即刷新」。", "Inicia sesión en Codex en el navegador y luego haz clic derecho en la barra y elige Actualizar ahora.", "Entre no Codex pelo navegador e depois clique com o botão direito na barra e escolha Atualizar agora.", "Im Browser bei Codex anmelden und dann mit Rechtsklick auf die Leiste „Jetzt aktualisieren“ wählen."],
            ["Could not start codex login."] =
                ["无法启动 codex login。", "No se pudo iniciar codex login.", "Não foi possível iniciar o codex login.", "codex login konnte nicht gestartet werden."],

            ["Sign in at cursor.com, or paste the cookie in Settings."] =
                ["登录 cursor.com，或在设置中粘贴 Cookie。", "Inicia sesión en cursor.com o pega la cookie en Configuración.", "Entre em cursor.com ou cole o cookie nas Configurações.", "Bei cursor.com anmelden oder das Cookie in den Einstellungen einfügen."],
            ["Sign in with codex login, or open it from Settings."] =
                ["运行 codex login，或从设置中打开登录。", "Inicia sesión con codex login o ábrelo desde Configuración.", "Entre com codex login ou abra pelo menu Configurações.", "Mit codex login anmelden oder es über die Einstellungen öffnen."],
            ["Rate limited. Will retry shortly."] =
                ["已达到速率限制。稍后会重试。", "Límite de velocidad alcanzado. Se reintentará pronto.", "Limite de taxa atingido. Tentaremos novamente em breve.", "Ratenlimit erreicht. Wird in Kürze erneut versucht."],
            ["Could not fetch usage."] =
                ["获取使用量失败。", "No se pudo obtener el uso.", "Não foi possível obter o uso.", "Nutzung konnte nicht abgerufen werden."],
            ["No endpoint"] = ["未设置 URL", "Sin endpoint", "Sem endpoint", "Kein Endpoint"],

            ["5h"] = ["5小时", "5 h", "5 h", "5 Std."],
            ["7d"] = ["7天", "7 d", "7 d", "7 T."],
            ["1d"] = ["1天", "1 d", "1 d", "1 T."],
            ["30d"] = ["30天", "30 d", "30 d", "30 T."],
            ["1y"] = ["1年", "1 a", "1 a", "1 J."],
            ["Rolling"] = ["滚动", "Móvil", "Móvel", "Rollierend"],
            ["Weekly"] = ["每周", "Semanal", "Semanal", "Wöchentlich"],
            ["Monthly"] = ["每月", "Mensual", "Mensal", "Monatlich"],
            ["starts on first use"] = ["首次使用时开始", "comienza al primer uso", "começa no primeiro uso", "beginnt bei der ersten Nutzung"],

            ["Requests"] = ["请求", "Solicitudes", "Solicitações", "Anfragen"],
            ["Cost"] = ["成本", "Coste", "Custo", "Kosten"],
            ["Tokens"] = ["令牌", "Tokens", "Tokens", "Token"],
            ["Cache"] = ["缓存", "Caché", "Cache", "Cache"],
            ["Credits"] = ["额度", "Créditos", "Créditos", "Guthaben"],
        };

    public static readonly IReadOnlyDictionary<string, string[]> Formats =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Approve this sign-in in the browser.\n\nCode: {0}"] =
                ["请在浏览器中批准此次登录。\n\n代码：{0}",
                 "Aprueba este inicio de sesión en el navegador.\n\nCódigo: {0}",
                 "Aprove este login no navegador.\n\nCódigo: {0}",
                 "Bestätige diese Anmeldung im Browser.\n\nCode: {0}"],
            ["in {0}d {1}h"] = ["{0}天{1}小时后", "en {0} d {1} h", "em {0} d {1} h", "in {0} T. {1} Std."],
            ["in {0}h {1}m"] = ["{0}小时{1}分钟后", "en {0} h {1} min", "em {0} h {1} min", "in {0} Std. {1} Min."],
            ["in {0}m"] = ["{0}分钟后", "en {0} min", "em {0} min", "in {0} Min."],
            [" ({0}/{1})"] = ["（{0}/{1}）", " ({0}/{1})", " ({0}/{1})", " ({0}/{1})"],
            [" ({0})"] = ["（{0}）", " ({0})", " ({0})", " ({0})"],
            ["Last {0} days"] = ["最近{0}天", "Últimos {0} días", "Últimos {0} dias", "Letzte {0} Tage"],
        };
}
