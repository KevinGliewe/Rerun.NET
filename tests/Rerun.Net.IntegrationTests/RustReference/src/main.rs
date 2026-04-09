use std::env;
use std::path::PathBuf;

fn main() -> Result<(), Box<dyn std::error::Error>> {
    let args: Vec<String> = env::args().collect();
    if args.len() < 3 {
        eprintln!("Usage: rrd-reference-gen <snippet_name> <output.rrd>");
        std::process::exit(1);
    }

    let snippet = &args[1];
    let output = PathBuf::from(&args[2]);

    match snippet.as_str() {
        "points3d_simple" => points3d_simple(&output)?,
        "points3d_props" => points3d_props(&output)?,
        "arrows3d_simple" => arrows3d_simple(&output)?,
        "boxes2d_simple" => boxes2d_simple(&output)?,
        "line_strips2d_simple" => line_strips2d_simple(&output)?,
        "line_strips3d_simple" => line_strips3d_simple(&output)?,
        "geo_points_simple" => geo_points_simple(&output)?,
        "clear_simple" => clear_simple(&output)?,
        "text_log_simple" => text_log_simple(&output)?,
        "scalars_simple" => scalars_simple(&output)?,
        "transform3d_simple" => transform3d_simple(&output)?,
        "minimal" => minimal(&output)?,
        _ => {
            eprintln!("Unknown snippet: {snippet}");
            std::process::exit(1);
        }
    }

    Ok(())
}

fn points3d_simple(output: &std::path::Path) -> Result<(), Box<dyn std::error::Error>> {
    let rec = rerun::RecordingStreamBuilder::new("rerun_example_points3d")
        .save(output.to_str().unwrap())?;
    rec.log("points", &rerun::Points3D::new([(0.0, 0.0, 0.0), (1.0, 1.0, 1.0)]))?;
    rec.flush_blocking();
    Ok(())
}

fn points3d_props(output: &std::path::Path) -> Result<(), Box<dyn std::error::Error>> {
    let rec = rerun::RecordingStreamBuilder::new("rerun_example_points3d_props")
        .save(output.to_str().unwrap())?;
    rec.log(
        "points",
        &rerun::Points3D::new([(1.0, 2.0, 3.0), (4.0, 5.0, 6.0), (7.0, 8.0, 9.0)])
            .with_colors([
                rerun::Color::from_unmultiplied_rgba(255, 0, 0, 255),
                rerun::Color::from_unmultiplied_rgba(0, 255, 0, 255),
                rerun::Color::from_unmultiplied_rgba(0, 0, 255, 255),
            ])
            .with_radii([0.5, 1.0, 0.25])
            .with_labels(["A", "B", "C"]),
    )?;
    rec.flush_blocking();
    Ok(())
}

fn arrows3d_simple(output: &std::path::Path) -> Result<(), Box<dyn std::error::Error>> {
    let rec = rerun::RecordingStreamBuilder::new("rerun_example_arrow3d")
        .save(output.to_str().unwrap())?;
    rec.log(
        "arrows",
        &rerun::Arrows3D::from_vectors([
            [1.0, 0.0, 0.0],
            [0.0, 1.0, 0.0],
            [0.0, 0.0, 1.0],
        ])
        .with_origins([
            rerun::Position3D::ZERO,
            rerun::Position3D::ZERO,
            rerun::Position3D::ZERO,
        ])
        .with_colors([
            rerun::Color::from_unmultiplied_rgba(255, 0, 0, 255),
            rerun::Color::from_unmultiplied_rgba(0, 255, 0, 255),
            rerun::Color::from_unmultiplied_rgba(0, 0, 255, 255),
        ]),
    )?;
    rec.flush_blocking();
    Ok(())
}

fn boxes2d_simple(output: &std::path::Path) -> Result<(), Box<dyn std::error::Error>> {
    let rec = rerun::RecordingStreamBuilder::new("rerun_example_box2d")
        .save(output.to_str().unwrap())?;
    rec.log(
        "simple",
        &rerun::Boxes2D::from_half_sizes([(1.0, 1.0)]).with_centers([(0.0, 0.0)]),
    )?;
    rec.flush_blocking();
    Ok(())
}

fn line_strips2d_simple(output: &std::path::Path) -> Result<(), Box<dyn std::error::Error>> {
    let rec = rerun::RecordingStreamBuilder::new("rerun_example_line_strip2d")
        .save(output.to_str().unwrap())?;
    let points = [[0., 0.], [2., 1.], [4., -1.], [6., 0.]];
    rec.log("strip", &rerun::LineStrips2D::new([points]))?;
    rec.flush_blocking();
    Ok(())
}

fn line_strips3d_simple(output: &std::path::Path) -> Result<(), Box<dyn std::error::Error>> {
    let rec = rerun::RecordingStreamBuilder::new("rerun_example_line_strip3d")
        .save(output.to_str().unwrap())?;
    let points = [
        [0., 0., 0.], [0., 0., 1.], [1., 0., 0.], [1., 0., 1.],
        [1., 1., 0.], [1., 1., 1.], [0., 1., 0.], [0., 1., 1.],
    ];
    rec.log("strip", &rerun::LineStrips3D::new([points]))?;
    rec.flush_blocking();
    Ok(())
}

fn geo_points_simple(output: &std::path::Path) -> Result<(), Box<dyn std::error::Error>> {
    let rec = rerun::RecordingStreamBuilder::new("rerun_example_geo_points")
        .save(output.to_str().unwrap())?;
    rec.log(
        "rerun_hq",
        &rerun::GeoPoints::from_lat_lon([(59.319221, 18.075631)])
            .with_radii([rerun::Radius::new_ui_points(10.0)])
            .with_colors([rerun::Color::from_unmultiplied_rgba(255, 0, 0, 255)]),
    )?;
    rec.flush_blocking();
    Ok(())
}

fn clear_simple(output: &std::path::Path) -> Result<(), Box<dyn std::error::Error>> {
    let rec = rerun::RecordingStreamBuilder::new("rerun_example_clear")
        .save(output.to_str().unwrap())?;
    rec.set_time_sequence("step", 0);
    rec.log("points", &rerun::Points3D::new([(1.0, 2.0, 3.0)]))?;
    rec.set_time_sequence("step", 1);
    rec.log("points", &rerun::Clear::flat())?;
    rec.flush_blocking();
    Ok(())
}

fn text_log_simple(output: &std::path::Path) -> Result<(), Box<dyn std::error::Error>> {
    let rec = rerun::RecordingStreamBuilder::new("rerun_example_text_log")
        .save(output.to_str().unwrap())?;
    rec.log(
        "logs",
        &rerun::TextLog::new("Hello from Rust").with_level(rerun::TextLogLevel::INFO),
    )?;
    rec.flush_blocking();
    Ok(())
}

fn scalars_simple(output: &std::path::Path) -> Result<(), Box<dyn std::error::Error>> {
    let rec = rerun::RecordingStreamBuilder::new("rerun_example_scalars")
        .save(output.to_str().unwrap())?;
    for i in 0..10 {
        rec.set_time_sequence("step", i);
        rec.log("plot/value", &rerun::Scalars::new([(i as f64 * 0.1).sin()]))?;
    }
    rec.flush_blocking();
    Ok(())
}

fn transform3d_simple(output: &std::path::Path) -> Result<(), Box<dyn std::error::Error>> {
    let rec = rerun::RecordingStreamBuilder::new("rerun_example_transform3d")
        .save(output.to_str().unwrap())?;
    rec.log(
        "transform",
        &rerun::Transform3D::default().with_translation([1.0, 2.0, 3.0]),
    )?;
    rec.flush_blocking();
    Ok(())
}

fn minimal(output: &std::path::Path) -> Result<(), Box<dyn std::error::Error>> {
    let rec = rerun::RecordingStreamBuilder::new("rerun_example_minimal")
        .save(output.to_str().unwrap())?;

    // 10x10x10 grid from -10 to 10, matching numpy mgrid[3 * [slice(-10, 10, 10j)]]
    let steps: Vec<f32> = (0..10).map(|i| -10.0 + (20.0 / 9.0) * i as f32).collect();
    let mut positions = Vec::new();
    let mut colors = Vec::new();
    let color_steps: Vec<u8> = (0..10).map(|i| (255.0 / 9.0 * i as f64) as u8).collect();

    for iz in 0..10 {
        for iy in 0..10 {
            for ix in 0..10 {
                positions.push(rerun::Position3D::new(steps[ix], steps[iy], steps[iz]));
                colors.push(rerun::Color::from_rgb(color_steps[ix], color_steps[iy], color_steps[iz]));
            }
        }
    }

    rec.log("my_points", &rerun::Points3D::new(positions).with_colors(colors).with_radii([0.5]))?;
    rec.flush_blocking();
    Ok(())
}

/*
// graph_lattice skipped - rerun Rust SDK graph types not in top-level reexports
fn graph_lattice(output: &std::path::Path) -> Result<(), Box<dyn std::error::Error>> {
    let rec = rerun::RecordingStreamBuilder::new("rerun_example_graph_lattice")
        .save(output.to_str().unwrap())?;

    let num_nodes = 10usize;
    let mut nodes = Vec::new();
    let mut colors = Vec::new();
    let mut labels = Vec::new();

    for y in 0..num_nodes {
        for x in 0..num_nodes {
            let idx = y * num_nodes + x;
            nodes.push(rerun::GraphNode::new(idx.to_string()));
            let r = (255.0 * x as f64 / (num_nodes - 1) as f64).round() as u8;
            let g = (255.0 * y as f64 / (num_nodes - 1) as f64).round() as u8;
            colors.push(rerun::Color::from_rgb(r, g, 0));
            labels.push(rerun::Text::from(format!("({x}, {y})")));
        }
    }

    rec.log_static(
        "/lattice",
        &rerun::GraphNodes::new(nodes).with_colors(colors).with_labels(labels),
    )?;

    let mut edges = Vec::new();
    for y in 0..num_nodes {
        for x in 0..num_nodes {
            if y > 0 {
                let source = (y - 1) * num_nodes + x;
                let target = y * num_nodes + x;
                edges.push(rerun::GraphEdge::new(source.to_string(), target.to_string()));
            }
            if x > 0 {
                let source = y * num_nodes + (x - 1);
                let target = y * num_nodes + x;
                edges.push(rerun::GraphEdge::new(source.to_string(), target.to_string()));
            }
        }
    }

    rec.log_static(
        "/lattice",
        &rerun::GraphEdges::new(edges).with_graph_type(rerun::GraphType::Directed),
    )?;

    rec.flush_blocking();
    Ok(())
}
*/
