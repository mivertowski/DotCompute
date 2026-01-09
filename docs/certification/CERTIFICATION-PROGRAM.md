# DotCompute Developer Certification Program

**Version**: 1.0.0
**Effective Date**: January 2026

---

## Program Overview

The DotCompute Certification Program validates developer expertise in GPU-accelerated computing with DotCompute. Three certification levels are available to match different skill levels and career goals.

---

## Certification Levels

### 🥉 DotCompute Associate (DCA)

**Target Audience**: Developers new to GPU computing
**Prerequisites**: Basic C# and .NET knowledge
**Exam Duration**: 90 minutes
**Questions**: 60 multiple choice
**Passing Score**: 70%
**Validity**: 2 years

#### Topics Covered

| Domain | Weight |
|--------|--------|
| 1. DotCompute Fundamentals | 25% |
| 2. Kernel Development | 25% |
| 3. Memory Management | 20% |
| 4. Backend Selection | 15% |
| 5. Basic Optimization | 15% |

#### Domain 1: DotCompute Fundamentals (25%)
- Architecture overview
- Installation and setup
- Project configuration
- Accelerator factory usage
- Basic async patterns

#### Domain 2: Kernel Development (25%)
- `[Kernel]` attribute usage
- Thread hierarchy (grid, block, thread)
- Thread index calculations
- Bounds checking patterns
- Analyzer warnings and fixes

#### Domain 3: Memory Management (20%)
- UnifiedBuffer<T> API
- Host-device transfers
- Async disposal patterns
- Memory pooling basics

#### Domain 4: Backend Selection (15%)
- Available backends
- Auto-selection behavior
- Platform compatibility
- Basic configuration

#### Domain 5: Basic Optimization (15%)
- Memory access patterns
- Avoiding common mistakes
- Performance measurement basics

---

### 🥈 DotCompute Professional (DCP)

**Target Audience**: Experienced developers using DotCompute in production
**Prerequisites**: DCA certification or equivalent experience
**Exam Duration**: 120 minutes
**Questions**: 50 multiple choice + 3 hands-on labs
**Passing Score**: 75%
**Validity**: 2 years

#### Topics Covered

| Domain | Weight |
|--------|--------|
| 1. Advanced Kernel Development | 20% |
| 2. Memory Optimization | 20% |
| 3. Multi-Backend Development | 15% |
| 4. Ring Kernels | 15% |
| 5. LINQ Extensions | 10% |
| 6. Debugging & Profiling | 10% |
| 7. Production Deployment | 10% |

#### Domain 1: Advanced Kernel Development (20%)
- Shared memory usage
- Warp-level primitives
- Barrier synchronization
- Memory ordering
- Complex kernel patterns

#### Domain 2: Memory Optimization (20%)
- Coalesced access patterns
- Bank conflict avoidance
- P2P transfers
- Memory pool tuning
- Zero-copy techniques

#### Domain 3: Multi-Backend Development (15%)
- Cross-platform code
- Backend-specific features
- Capability detection
- Fallback strategies

#### Domain 4: Ring Kernels (15%)
- Actor model concepts
- Message type design
- Runtime management
- Messaging patterns
- WSL2 considerations

#### Domain 5: LINQ Extensions (10%)
- GPU queryable API
- Operation support
- Kernel fusion
- Performance trade-offs

#### Domain 6: Debugging & Profiling (10%)
- Debug compilation mode
- CPU fallback debugging
- Profiling tools
- Performance analysis

#### Domain 7: Production Deployment (10%)
- Configuration management
- Error handling
- Monitoring setup
- Scaling considerations

#### Hands-On Labs

**Lab 1**: Implement optimized matrix multiplication kernel
- Time: 20 minutes
- Criteria: Correct output, shared memory usage, >80% baseline performance

**Lab 2**: Design multi-stage data pipeline with Ring Kernels
- Time: 25 minutes
- Criteria: Working pipeline, proper message types, clean shutdown

**Lab 3**: Debug and optimize slow kernel
- Time: 15 minutes
- Criteria: Identify bottleneck, apply fix, >50% improvement

---

### 🥇 DotCompute Expert (DCE)

**Target Audience**: Architects and senior engineers designing large-scale GPU systems
**Prerequisites**: DCP certification + 1 year production experience
**Exam Duration**: 180 minutes
**Format**: Case study analysis + implementation + oral defense
**Passing Score**: 80%
**Validity**: 3 years

#### Topics Covered

| Domain | Weight |
|--------|--------|
| 1. Architecture Design | 25% |
| 2. Performance Engineering | 25% |
| 3. Multi-GPU Systems | 20% |
| 4. Advanced Algorithms | 15% |
| 5. Enterprise Integration | 15% |

#### Domain 1: Architecture Design (25%)
- System architecture patterns
- Scalability considerations
- Technology selection
- Trade-off analysis
- Cost optimization

#### Domain 2: Performance Engineering (25%)
- Deep performance analysis
- Hardware-aware optimization
- Benchmarking methodology
- Regression detection
- Performance budgeting

#### Domain 3: Multi-GPU Systems (20%)
- Multi-GPU architectures
- Data partitioning strategies
- Communication optimization
- NCCL integration
- Fault tolerance

#### Domain 4: Advanced Algorithms (15%)
- Automatic differentiation
- Sparse matrix operations
- FFT optimization
- Custom reduction patterns
- Domain-specific optimizations

#### Domain 5: Enterprise Integration (15%)
- CI/CD for GPU workloads
- Observability at scale
- Security considerations
- Compliance requirements
- Team enablement

#### Exam Format

**Part 1: Case Study Analysis (60 min)**
- Review complex system requirements
- Identify design constraints
- Propose architecture solution
- Document trade-offs

**Part 2: Implementation (90 min)**
- Implement key components
- Demonstrate optimization techniques
- Write production-quality code
- Include tests

**Part 3: Oral Defense (30 min)**
- Present solution to panel
- Justify design decisions
- Respond to alternatives
- Discuss production considerations

---

## Exam Policies

### Registration
- Online registration at certifications.dotcompute.io
- Payment required at registration
- Reschedule up to 48 hours before exam

### Exam Delivery
- Online proctored exams (DCA, DCP)
- In-person at testing centers (DCE)
- Government-issued ID required

### Pricing

| Certification | First Attempt | Retake |
|---------------|---------------|--------|
| DCA | $150 | $100 |
| DCP | $350 | $250 |
| DCE | $750 | $500 |

*Note: Discounts available for students, educators, and volume purchases*

### Retake Policy
- Wait 14 days between attempts
- Maximum 3 attempts per 12 months
- Full payment for each attempt

### Recertification
- Complete before expiration
- Recertification exam (shorter)
- Or upgrade to next level

---

## Study Resources

### Official Materials

| Resource | Format | Access |
|----------|--------|--------|
| DotCompute Documentation | Online | Free |
| Video Tutorial Series | Video | Free |
| Sample Applications | Code | Free |
| Practice Exams | Online | $50-100 |
| Study Guide (PDF) | PDF | $30-50 |

### Recommended Preparation

#### DCA (40-60 hours)
1. Complete Getting Started guide
2. Watch Video Tutorials 1-4
3. Build Samples 1-2
4. Take practice exam
5. Review weak areas

#### DCP (80-100 hours)
1. Complete all documentation
2. Watch all Video Tutorials
3. Build all Samples
4. Production project experience
5. Take practice exam
6. Lab practice (20+ hours)

#### DCE (150+ hours)
1. All DCP requirements
2. Architecture case studies
3. Multi-GPU project experience
4. Performance optimization practice
5. Oral presentation practice
6. Industry best practices research

---

## Certification Maintenance

### Continuing Education
- Earn CEU credits to maintain active status
- 20 CEUs per year for DCA/DCP
- 30 CEUs per year for DCE

### CEU Activities

| Activity | CEUs |
|----------|------|
| DotCompute conference attendance | 10 |
| Official webinar | 2 |
| Community presentation | 5 |
| Published blog post/article | 3 |
| GitHub contribution (merged) | 2 |
| Answering community questions | 1/answer |

---

## Benefits

### For Individuals
- Industry-recognized credential
- Salary premium (avg +15-25%)
- Career advancement
- Community recognition
- Access to exclusive resources

### For Organizations
- Validated team competency
- Reduced hiring risk
- Partner program eligibility
- Professional development path
- Quality assurance

### Certification Badges

Certified individuals receive:
- Digital badge (Credly)
- Verification page
- LinkedIn certification
- Certificate (PDF)

---

## Program Governance

### Exam Development
- Subject matter expert panel
- Regular item analysis
- Annual content review
- Community feedback integration

### Quality Assurance
- Statistical reliability analysis
- Cut score validation
- Bias review
- Regular updates for new versions

### Appeals Process
1. Submit appeal within 30 days
2. Review by certification board
3. Decision within 14 days
4. One appeal per exam attempt

---

## Contact

- **Website**: certifications.dotcompute.io
- **Email**: certifications@dotcompute.io
- **Support**: support@dotcompute.io

---

**Last Updated**: January 5, 2026
**Program Effective**: Q1 2026
