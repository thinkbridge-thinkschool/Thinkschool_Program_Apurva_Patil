## Day 5 — Task 1: Slow Endpoint Diagnosis

### Before Fix
- Endpoint: GET /Quotes/slow-nplusone
- Duration: 634.84ms
- Total Spans: 53

The trace showed the slow span was repeated DB calls 
(53 spans) because of an N+1 query — EF Core was hitting 
the database once per quote row to fetch related data, 
instead of using a JOIN. I fixed it by adding 
.Include() for eager loading, which collapsed 53 spans 
into 3 and reduced response time from 634ms to 17ms 
— a 97% improvement.

### After Fix  
- Endpoint: GET /Quotes/slow-nplusone
- Duration: 17.78ms
- Total Spans: 3