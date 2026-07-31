mod app_config;
mod rdplaunch;
mod remote_assist;
mod service_checker;
mod drive_sync;
mod pst_indexer;

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
    .plugin(tauri_plugin_dialog::init())
    .plugin(tauri_plugin_fs::init())
    .plugin(tauri_plugin_sql::Builder::default().build())
    // 3. Invoke handler for frontend commands
    .invoke_handler(tauri::generate_handler![
      app_config::read_app_config,
      app_config::write_app_config,
      app_config::get_windows_user,
      rdplaunch::launch_rdp,
      rdplaunch::launch_nav,
      rdplaunch::suppress_rdp_warnings,
      rdplaunch::get_rdp_history,
      rdplaunch::delete_rdp_history,
      service_checker::check_services,
      service_checker::start_service,
      service_checker::stop_service,
      service_checker::restart_service,
      remote_assist::remote_assist_pointer,
      drive_sync::run_rclone_copyto,
      pst_indexer::get_pst_indexer_status,
      pst_indexer::run_pst_auto_fix,
      pst_indexer::rebuild_pst_search_catalog,
      pst_indexer::run_scanpst_repair_staging,
      pst_indexer::close_outlook_process,
      pst_indexer::restore_repaired_pst,
      pst_indexer::get_scanpst_repair_log,
      pst_indexer::reset_windows_search_db,
      pst_indexer::set_turbo_indexing_mode,
      pst_indexer::register_pst_crawl_scope,
      pst_indexer::run_systematic_pst_repair_routine,
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
