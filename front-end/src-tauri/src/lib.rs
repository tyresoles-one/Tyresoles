mod app_config;
mod rdplaunch;
mod remote_assist;
mod service_checker;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
  tauri::Builder::default()
    // 1. Logging must be the FIRST plugin — it enables all subsequent log! calls.
    //    Targets: application log file + stdout + browser DevTools console.
    //    Log file location: %LOCALAPPDATA%\com.tyresoles.app\logs\app.log
    .plugin(
      tauri_plugin_log::Builder::default()
        .level(log::LevelFilter::Debug)
        .targets([
          tauri_plugin_log::Target::new(tauri_plugin_log::TargetKind::Stdout),
          tauri_plugin_log::Target::new(tauri_plugin_log::TargetKind::LogDir {
            file_name: Some("app".into()),
          }),
          tauri_plugin_log::Target::new(tauri_plugin_log::TargetKind::Webview),
        ])
        .build(),
    )
    // 2. Auto-update + process plugins
    .plugin(tauri_plugin_updater::Builder::new().build())
    .plugin(tauri_plugin_process::init())
    .plugin(tauri_plugin_notification::init())
    // 3. Invoke handler for frontend commands
    .invoke_handler(tauri::generate_handler![
      app_config::read_app_config,
      app_config::write_app_config,
      app_config::get_windows_user,
      rdplaunch::launch_rdp,
      rdplaunch::launch_nav,
      service_checker::check_services,
      service_checker::start_service,
      service_checker::stop_service,
      service_checker::restart_service,
      remote_assist::remote_assist_pointer,
    ])
    // 4. Setup hook — runs after plugins are initialized.
    //    Eagerly create the config file so it exists before the frontend loads.
    .setup(|_app| {
      log::info!("[setup] Tauri app starting...");
      let config = app_config::init_config();
      log::info!("[setup] Config initialized. backendBaseUrl={}", config.backend_base_url);
      Ok(())
    })
    .run(tauri::generate_context!())
    .expect("error while running tauri application");
}
