import {
  CircleMarker,
  MapContainer,
  TileLayer,
  useMapEvents,
} from 'react-leaflet'
import 'leaflet/dist/leaflet.css'

function MapClickHandler({ onLocationSelect }) {
  useMapEvents({
    click(event) {
      onLocationSelect(event.latlng.lat, event.latlng.lng)
    },
  })

  return null
}

export default function LocationPicker({
  latitude,
  longitude,
  onLocationSelect,
}) {
  const latitudeValue = Number(latitude)
  const longitudeValue = Number(longitude)
  const hasValidPosition =
    latitude !== '' &&
    longitude !== '' &&
    Number.isFinite(latitudeValue) &&
    Number.isFinite(longitudeValue) &&
    latitudeValue >= -90 &&
    latitudeValue <= 90 &&
    longitudeValue >= -180 &&
    longitudeValue <= 180
  const position = hasValidPosition
    ? [latitudeValue, longitudeValue]
    : null

  return (
    <MapContainer
      center={[6.9271, 79.8612]}
      zoom={12}
      scrollWheelZoom={false}
      className="location-map"
    >
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      />

      <MapClickHandler onLocationSelect={onLocationSelect} />

      {position && (
        <CircleMarker
          center={position}
          radius={8}
          pathOptions={{
            color: '#ffffff',
            fillColor: '#176b55',
            fillOpacity: 1,
            weight: 3,
          }}
        />
      )}
    </MapContainer>
  )
}
