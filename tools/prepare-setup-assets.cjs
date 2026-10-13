// Preserve the Figma SVG sources and rasterize them for Unity's texture importer.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require(process.env.SETUP_SHARP_MODULE || 'sharp');
const directory = path.resolve(__dirname, '../Assets/UI/Setup');
async function main() {
    for (const name of fs.readdirSync(directory).filter(name => name.endsWith('.svg'))) {
        await sharp(path.join(directory, name), { density: 288 })
            .png().toFile(path.join(directory, name.replace(/\.svg$/, '.png')));
    }
}
main().catch(error => { console.error(error); process.exitCode = 1; });
