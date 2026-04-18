//! Remote assist: inject mouse input from normalized viewer coordinates (0..1).
//! Maps to primary display pixel size. Windows desktop host only.

use enigo::{Button, Coordinate, Direction, Enigo, Mouse, Settings};

fn primary_size() -> Result<(u32, u32), String> {
    let displays = display_info::DisplayInfo::all().map_err(|e| e.to_string())?;
    if displays.is_empty() {
        return Err("no display".to_string());
    }
    let d = displays
        .iter()
        .find(|d| d.is_primary)
        .unwrap_or(&displays[0]);
    Ok((d.width, d.height))
}

#[tauri::command]
pub fn remote_assist_pointer(x_norm: f64, y_norm: f64, action: String, delta_y: Option<i32>) -> Result<(), String> {
    let (sw, sh) = primary_size()?;
    let px = (x_norm.clamp(0.0, 1.0) * sw as f64) as i32;
    let py = (y_norm.clamp(0.0, 1.0) * sh as f64) as i32;
    let mut enigo = Enigo::new(&Settings::default()).map_err(|e| e.to_string())?;
    enigo
        .move_mouse(px, py, Coordinate::Abs)
        .map_err(|e| e.to_string())?;
    let btn = Button::Left;
    match action.as_str() {
        "down" => enigo.button(btn, Direction::Press).map_err(|e| e.to_string()),
        "up" => enigo.button(btn, Direction::Release).map_err(|e| e.to_string()),
        "wheel" => {
            if let Some(dy) = delta_y {
                // Negative because enigo uses positive for scroll up (or vice-versa depending on platform)
                enigo.scroll(-dy, enigo::Axis::Vertical).map_err(|e| e.to_string())
            } else {
                Ok(())
            }
        }
        "move" | _ => Ok(()),
    }
}
