/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */
package org.apache.reef.wake.test.examples;

import org.apache.reef.wake.examples.join.BlockingJoin;
import org.apache.reef.wake.examples.join.EventPrinter;
import org.apache.reef.wake.examples.join.TupleEvent;
import org.apache.reef.wake.examples.join.TupleSource;
import org.junit.Test;

/**
 * Tests for BlockingJoin.
 */
public class TestBlockingJoin {
  @Test
  public void testJoin() throws Exception {
    final EventPrinter<TupleEvent> printer = new EventPrinter<>();
    final BlockingJoin join = new BlockingJoin(printer);
    final TupleSource left = new TupleSource(join.wireLeft(), 256, 8, true);
    final TupleSource right = new TupleSource(join.wireRight(), 256, 8, false);
    left.close();
    right.close();
  }

}
