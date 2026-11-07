# Historical sources

Every fact placed in `Assets/Data/*.json` must be traceable to an entry here. Format: what was used, where, link, date accessed.

- (Phase 0) `figures.json` carried over from the Three.js prototype; personalities and goals are dramatised from general knowledge of the Anarchy (Gesta Stephani; William of Malmesbury, *Historia Novella*; Anglo-Saxon Chronicle, Peterborough continuation). To be re-verified and cited properly in Phase 3.
- (Phase 1) Coastline of Great Britain: `datasets/geo-countries` (https://github.com/datasets/geo-countries, countries.geojson, feature GBR), derived from Natural Earth, public domain. Accessed 2026-11-07. Processed into `UnityProject/Assets/Data/england_coast.json`.
- (Phase 1) City coordinates (London, Winchester, Oxford, Wallingford, Bristol, Gloucester, Lincoln) from standard gazetteer latitude/longitude; upland and river positions placed by hand from a physical map of England and Wales and scaled stylistically.
- (Phase 1) Start date: Stephen was crowned at Westminster on 22 December 1135, a Sunday (Julian calendar). Canonical hours (Prime, Terce, Sext, None, Vespers, Compline) are the monastic day offices; parish bells rang fewer and less regular hours, a simplification noted for Phase 4's village day.
