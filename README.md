# Optimizer persister

The worker consumes two durable RabbitMQ queues:

- `optimizer.history` (`RabbitMq:QueueName`): existing optimizer session and request history events.
- `optimizer.fielddata` (`RabbitMq:FieldDataQueueName`): individual field measurements.

Each field measurement is a JSON object with `Station`, `Variable`, numeric `Value`,
`Type`, and `Timestamp` (an ISO 8601 date/time with time-zone offset). For customer
measurements, also supply `Customer` as the customer number already stored in
`customers.CustomerNumber`:

```json
{
  "Station": "Station01",
  "Variable": "PowerWatt",
  "Value": 22.4,
  "Type": "CustomerMeter",
  "Timestamp": "2026-10-11T12:00:00+02:00",
  "Customer": "12345"
}
```

`Type` must be one of `CustomerMeter`, `PvYield`, `StationConsumption`, or
`DirectLinePower`. Omit `Customer` for the other three types. Use `Variable` to
identify the measured quantity and its unit; the persister does not convert values.
The worker resolves the customer number to the existing customer ID, stores the
measurement in `optimizer_field_data`, and normalizes its timestamp to UTC.
Messages with invalid values or unknown customers are retried up to
`RabbitMq:MaxRetryCount` and then rejected without requeue.

## Grafana dashboards

Import `grafana/dashboards/optimizer-current-session.json` and
`grafana/dashboards/optimizer-lifetime.json` through Grafana's **Dashboards > New >
Import**. Create a **PostgreSQL data source** pointing to the same database as
the persister worker first; map **Optimizer PostgreSQL** to it once when
importing each JSON file. The dashboards have no data-source selector.
Grafana's own PostgreSQL storage configured
with `GF_DATABASE_*` does **not** automatically create a query data source. Use
read-only database credentials for the data source; do not store passwords in
the dashboard JSON. The running local stack currently stores the new field
measurements in `dlstannhausen_test`; `soweit_optimizer` is a different database
and does not contain those measurements. Check the worker's active connection
configuration when choosing a data source.

The current-session dashboard defaults to the most recently created *open*
session (`EndedAtUtc IS NULL`). Its session selector also allows inspecting
completed sessions; extend the dashboard time range beyond the default 24 hours
when viewing an older one. Field measurements have no `SessionId`, so they
are correlated with a session only by their timestamps falling between the
session start and end. With no open session, "Aktuelle Session" shows no session
data rather than silently selecting an old one.

The lifetime dashboard initially shows the whole recorded period from January
2026 onward; the Grafana time picker controls the time-series and
switch-transition panels. Total counts and the session table include all
records regardless of the time picker. Long ranges are downsampled to
interval averages for performance. Both dashboards display `ShouldSwitch`
as the optimizer's *intended* switching decision, not a measured contact
state. "Calculated PV surplus" is `max(AvailablePvPowerWatt -
ConsumedPowerWatt, 0)` from `run` events, not measured export. Field power
charts for PV, stations, and DLS include only messages whose `Variable` is
`PowerWatt`. The customer chart includes `Leistung` (as sent by the current
customer meters) and `PowerWatt`, keeps those series separate, and does not
assume a unit for `Leistung`. The **Benötigte Leistung je Kunde** charts
instead use the optimizer's `RequiredPowerWatt` from `run` events; the
current-session dashboard also lists the last per-customer demand beside
the corresponding intended switching decision.
